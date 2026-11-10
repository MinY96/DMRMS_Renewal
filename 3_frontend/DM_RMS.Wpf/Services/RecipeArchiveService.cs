using System.IO;
using System.IO.Compression;
using System.Text.Json;
using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Services;

/// <summary>
/// Backend가 반환한 Recipe ZIP을 검증하고 사용자 지정 백업 폴더에 배치합니다.
/// </summary>
/// <remarks>
/// Backend ZIP 내부 경로는 <c>장비명/Recipe명/파일...</c> 형식입니다.
/// Backend가 반환한 ZIP을 검증한 뒤 Recipe별로 원자적으로 교체하여
/// 최종적으로 <c>백업루트/장비명/Recipe명</c> 구조로 저장합니다.
/// </remarks>
public sealed class RecipeArchiveService
{
    private const string ManifestFileName = "result.json";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// ZIP 바이트를 임시 파일로 저장하고 Recipe 폴더를 최종 백업 경로에 반영합니다.
    /// </summary>
    /// <param name="download">API에서 받은 ZIP 파일명과 바이트 데이터입니다.</param>
    /// <param name="destinationRoot">사용자가 선택한 최종 백업 루트 폴더입니다.</param>
    /// <returns>백업 루트 경로와 ZIP 내부 <c>result.json</c> 처리 결과입니다.</returns>
    /// <exception cref="ArgumentException">백업 경로가 비어 있을 때 발생합니다.</exception>
    /// <exception cref="InvalidDataException">ZIP 구조 또는 <c>result.json</c>이 유효하지 않을 때 발생합니다.</exception>
    public RecipeArchiveExtractionResult Extract(
        DownloadFileResult download,
        string destinationRoot)
    {
        ArgumentNullException.ThrowIfNull(download);

        if (string.IsNullOrWhiteSpace(destinationRoot))
            throw new ArgumentException("백업 경로가 비어 있습니다.", nameof(destinationRoot));

        if (download.Content.Length == 0)
            throw new InvalidDataException("서버에서 빈 ZIP 파일을 반환했습니다.");

        var destinationRootFullPath = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(destinationRootFullPath);

        var safeFileName = Path.GetFileName(download.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = $"recipe_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "DM_RMS",
            Guid.NewGuid().ToString("N"));
        var tempZipPath = Path.Combine(tempRoot, safeFileName);

        // 최종 백업 폴더와 같은 볼륨에서 Directory.Move가 수행되도록
        // staging 폴더를 사용자 지정 백업 루트 아래에 생성합니다.
        var stagingRoot = Path.Combine(
            destinationRootFullPath,
            $".dm_rms_staging_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(stagingRoot);

        try
        {
            File.WriteAllBytes(tempZipPath, download.Content);

            using var archive = ZipFile.OpenRead(tempZipPath);
            var manifest = ReadManifest(archive);
            var stagedRecipes = ExtractRecipeFilesToStaging(archive, stagingRoot);
            CommitStagedRecipes(stagedRecipes, destinationRootFullPath);

            return new RecipeArchiveExtractionResult
            {
                DestinationDirectory = destinationRootFullPath,
                Manifest = manifest
            };
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
            TryDeleteDirectory(tempRoot);
        }
    }

    /// <summary>
    /// ZIP 내부의 처리 결과 파일을 읽습니다.
    /// </summary>
    /// <param name="archive">열려 있는 Recipe ZIP입니다.</param>
    /// <returns>역직렬화된 Recipe 처리 결과입니다.</returns>
    private RecipeArchiveManifest ReadManifest(ZipArchive archive)
    {
        var entry = archive.Entries.FirstOrDefault(item =>
            string.Equals(
                NormalizeZipPath(item.FullName),
                ManifestFileName,
                StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            throw new InvalidDataException("ZIP 파일에 result.json이 없습니다.");

        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<RecipeArchiveManifest>(stream, _jsonOptions);
        return manifest ?? throw new InvalidDataException("result.json 역직렬화에 실패했습니다.");
    }

    /// <summary>
    /// ZIP의 Recipe 파일을 staging 영역에 안전하게 압축 해제합니다.
    /// </summary>
    /// <param name="archive">Backend가 반환한 ZIP입니다.</param>
    /// <param name="stagingRoot">임시 압축 해제 루트입니다.</param>
    /// <returns>압축 해제된 Recipe별 staging 경로 정보입니다.</returns>
    /// <remarks>
    /// ZIP 내부 경로의 첫 번째 요소는 장비명, 두 번째 요소는 Recipe명으로 해석합니다.
    /// </remarks>
    private static IReadOnlyCollection<StagedRecipe> ExtractRecipeFilesToStaging(
        ZipArchive archive,
        string stagingRoot)
    {
        var stagedRecipes = new Dictionary<string, StagedRecipe>(StringComparer.OrdinalIgnoreCase);
        var stagingRootFullPath = EnsureTrailingSeparator(Path.GetFullPath(stagingRoot));

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryPath = NormalizeZipPath(entry.FullName);

            if (string.IsNullOrWhiteSpace(normalizedEntryPath)
                || string.Equals(normalizedEntryPath, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pathParts = normalizedEntryPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 2)
            {
                throw new InvalidDataException(
                    $"Recipe ZIP 경로는 '장비명/Recipe명' 구조여야 합니다: {entry.FullName}");
            }

            var equipmentName = ValidatePathSegment(pathParts[0], "장비명");
            var recipeName = ValidatePathSegment(pathParts[1], "Recipe명");
            var recipeKey = $"{equipmentName}\u001f{recipeName}";

            var recipeStagingDirectory = Path.Combine(
                stagingRoot,
                equipmentName,
                recipeName);

            if (!stagedRecipes.ContainsKey(recipeKey))
            {
                stagedRecipes.Add(
                    recipeKey,
                    new StagedRecipe(
                        equipmentName,
                        recipeName,
                        recipeStagingDirectory));
            }

            // 장비명과 Recipe명 이후의 상대 경로만 Recipe 폴더 아래에 저장합니다.
            var relativeParts = pathParts.Skip(2).ToArray();
            if (relativeParts.Length == 0)
            {
                Directory.CreateDirectory(recipeStagingDirectory);
                continue;
            }

            foreach (var part in relativeParts)
                ValidatePathSegment(part, "Recipe 내부 경로");

            var relativePath = Path.Combine(relativeParts);
            var outputPath = Path.GetFullPath(Path.Combine(recipeStagingDirectory, relativePath));

            // ZIP Slip 방지: 계산된 경로가 staging 루트 밖으로 나갈 수 없습니다.
            if (!outputPath.StartsWith(stagingRootFullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"허용되지 않은 ZIP 경로입니다: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            entry.ExtractToFile(outputPath, overwrite: true);
        }

        return stagedRecipes.Values.ToArray();
    }

    /// <summary>
    /// staging 영역의 Recipe 폴더를 최종 백업 경로로 교체합니다.
    /// </summary>
    /// <param name="stagedRecipes">압축 해제가 완료된 Recipe 폴더 목록입니다.</param>
    /// <param name="destinationRoot">사용자 지정 백업 루트입니다.</param>
    private static void CommitStagedRecipes(
        IEnumerable<StagedRecipe> stagedRecipes,
        string destinationRoot)
    {
        foreach (var stagedRecipe in stagedRecipes)
        {
            var destinationDirectory = Path.Combine(
                destinationRoot,
                stagedRecipe.EquipmentName,
                stagedRecipe.RecipeName);

            ReplaceDirectorySafely(
                stagedRecipe.StagingDirectory,
                destinationDirectory);
        }
    }

    /// <summary>
    /// 기존 Recipe 폴더를 새 백업 폴더로 안전하게 교체합니다.
    /// </summary>
    /// <param name="sourceDirectory">새로 압축 해제된 staging Recipe 폴더입니다.</param>
    /// <param name="destinationDirectory">최종 Recipe 백업 폴더입니다.</param>
    /// <remarks>
    /// 기존 폴더를 임시 이름으로 이동한 후 새 폴더를 배치합니다. 새 폴더 배치에
    /// 실패하면 기존 폴더를 복원하므로, 단순 재귀 복사보다 불완전 백업 가능성이 낮습니다.
    /// </remarks>
    private static void ReplaceDirectorySafely(
        string sourceDirectory,
        string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"백업할 Recipe 폴더가 없습니다: {sourceDirectory}");

        var destinationParent = Path.GetDirectoryName(destinationDirectory)
            ?? throw new InvalidOperationException("최종 Recipe 상위 경로를 계산할 수 없습니다.");

        Directory.CreateDirectory(destinationParent);

        var previousDirectory = Path.Combine(
            destinationParent,
            $".{Path.GetFileName(destinationDirectory)}.previous_{Guid.NewGuid():N}");

        var previousMoved = false;

        try
        {
            if (Directory.Exists(destinationDirectory))
            {
                Directory.Move(destinationDirectory, previousDirectory);
                previousMoved = true;
            }

            Directory.Move(sourceDirectory, destinationDirectory);

            if (previousMoved)
                Directory.Delete(previousDirectory, recursive: true);
        }
        catch
        {
            // 새 폴더가 일부 생성된 경우 제거하고 기존 백업을 복구합니다.
            if (Directory.Exists(destinationDirectory))
                TryDeleteDirectory(destinationDirectory);

            if (previousMoved && Directory.Exists(previousDirectory))
                Directory.Move(previousDirectory, destinationDirectory);

            throw;
        }
    }

    /// <summary>
    /// 단일 폴더명으로 사용할 수 있는 값인지 검증합니다.
    /// </summary>
    /// <param name="value">검증할 경로 요소입니다.</param>
    /// <param name="displayName">예외 메시지에 사용할 항목명입니다.</param>
    /// <returns>검증이 완료된 원본 문자열입니다.</returns>
    private static string ValidatePathSegment(string value, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException($"유효하지 않은 {displayName}입니다: {value}");
        }

        return value;
    }

    /// <summary>
    /// ZIP 경로 구분자를 슬래시로 통일합니다.
    /// </summary>
    private static string NormalizeZipPath(string path)
        => path.Replace('\\', '/').Trim('/');

    /// <summary>
    /// 디렉터리 접두 경로 비교를 위해 마지막 구분자를 보장합니다.
    /// </summary>
    private static string EnsureTrailingSeparator(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
           + Path.DirectorySeparatorChar;

    /// <summary>
    /// 임시 디렉터리를 가능한 경우 제거합니다.
    /// </summary>
    /// <param name="path">삭제 대상 디렉터리입니다.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더 정리 실패는 완료된 Recipe 백업 결과를 실패로 변경하지 않습니다.
        }
        catch (UnauthorizedAccessException)
        {
            // 백신 또는 인덱싱 프로세스가 잠시 점유하는 경우를 허용합니다.
        }
    }

    /// <summary>
    /// staging 영역에 압축 해제된 단일 Recipe 폴더 정보입니다.
    /// </summary>
    private sealed record StagedRecipe(
        string EquipmentName,
        string RecipeName,
        string StagingDirectory);
}
