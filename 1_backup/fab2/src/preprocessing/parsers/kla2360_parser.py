"""KLA2360 XML parameter extraction; explicit error when required hierarchy is absent."""

from __future__ import annotations
from pathlib import Path
from typing import Any
from src.core.convert import xml_to_dict
from src.preprocessing.parsers.parser import Parser


class KLA2360Parser(Parser):
    def parse(self, file_path: Path) -> dict[str, Any]:
        """KLA2365 Sensitivity1.xml에서 광학·Threshold Parameter를 추출한다.

        Args:
            self: 현재 클래스 인스턴스.
            file_path: 분석 대상 파일 경로.

        Returns:
            dict[str, Any]: 처리 결과를 반환한다."""
        data = xml_to_dict(file_path)
        root = data["RECIPE_COMPONENT_NEW"]
        photon = root["OPTICS"]["STEALTH_OPTICS"]["PHOTON_OPTICS"]
        sensitivity = root["SENSITIVITY"]
        params = {
            "pixel": self.leaf(photon.get("PIXEL_SIZE")),
            "focus_offset": self.leaf(photon.get("OBJECTFOCUS_OFFSET")),
            "filter": self.leaf(sensitivity.get("DIFF_FILTER")),
            "size_sieve": self.leaf(sensitivity.get("SIZE_SIEVE")),
            "mode": None,
        }
        images = sensitivity["IMAGE_COMPUTER"]
        details = []
        seen_mode = set()
        if isinstance(images, dict):
            images = [images]
        for image in images:
            mode = self.leaf(image.get("THRESHOLD_MODE"))
            info = {"mode": mode, "threshold": None}
            if mode == "AUTO":
                threshold_item = image["SAT_DATA"]["SAT_THRESHOLDS"]
                if isinstance(threshold_item, list):
                    info["threshold"] = self.leaf(
                        threshold_item[0].get("OFFSET_THRESHOLDS")
                    ).split(" ")[0]
            elif mode == "SAT":
                sat_data = image["SAT_DATA"]
                frontend = sat_data["FRONT_END"]
                mappings = (
                    (self.leaf(frontend.get("MEAN_BREAK_POINTS")), "mean_break_point_"),
                    (self.leaf(frontend.get("SEGMENT_MAP")[0]), "segmentmap_"),
                )
                for items, prefix in mappings:
                    for i, item in enumerate(self.three(items)):
                        info[f"{prefix}{i+1}"] = item

                sat_thresholds = sat_data["SAT_THRESHOLDS"]
                if isinstance(sat_thresholds, list):
                    sat_thresholds = sat_thresholds[0]
                mappings = (
                    (self.leaf(sat_thresholds.get("MAX_THRESHOLDS")), "th_max_s"),
                    (self.leaf(sat_thresholds.get("OFFSET_THRESHOLDS")), "th_offset_s"),
                    (
                        self.leaf(sat_thresholds.get("TARGET_DENSITIES")),
                        "density_target_s",
                    ),
                )
                for items, prefix in mappings:
                    for i, item in enumerate(self.four(items)):
                        info[f"{prefix}{i}"] = item

            if "mode" in info and info["mode"] not in seen_mode:
                details.append(info)
                seen_mode.add(info["mode"])  # 추가된

        if len(details) == 1:
            target_dict = details[0]
        else:
            target_dict = next(item for item in details if item["mode"] == "SAT")
            auto_value = next(
                item["threshold"] for item in details if item["mode"] == "AUTO"
            )
            target_dict["threshold"] = auto_value

        params.update(target_dict)
        return dict(sorted(params.items()))
