"""FAB1 DM_RMS 서버 실행 진입점.

운영 환경에서는 ``waitress-serve --call app:create_app`` 사용을 권장한다.
"""

from src import create_app
from src.config.config import DEBUG_MODE, SERVER_HOST, SERVER_PORT

app = create_app()

if __name__ == "__main__":
    app.run(host=SERVER_HOST, port=SERVER_PORT, debug=DEBUG_MODE)
