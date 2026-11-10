"""FAB2 Flask 애플리케이션 팩토리."""

from __future__ import annotations

from flask import Flask
from flask_cors import CORS

from src.core.response import ok
from src.config.config import CURRENT_FAB, SERVER_NAME
from src.routes.auth import auth_bp
from src.routes.dashboard import dashboard_bp
from src.routes.recipe import recipe_bp
from src.routes.master import master_bp
from src.routes.non_matching import non_matching_bp
from src.routes.no_master import no_master_bp
from src.routes.exclude import exclude_bp
from src.routes.usage import usage_bp


def create_app() -> Flask:
    """FAB2 API Flask 애플리케이션을 생성하고 Blueprint를 등록한다.

    Returns:
        Route와 CORS 설정이 완료된 Flask 애플리케이션.
    """
    app = Flask(__name__)
    CORS(app)

    app.register_blueprint(auth_bp, url_prefix="/api/auth")
    app.register_blueprint(recipe_bp, url_prefix="/api/recipe")
    app.register_blueprint(master_bp, url_prefix="/api/master")
    app.register_blueprint(dashboard_bp, url_prefix="/api/dashboard")
    app.register_blueprint(non_matching_bp, url_prefix="/api/non_matching_detail")
    app.register_blueprint(no_master_bp, url_prefix="/api/no_master_detail")
    app.register_blueprint(exclude_bp, url_prefix="/api/exclude_detail")
    app.register_blueprint(usage_bp, url_prefix="/api/usage")

    @app.get("/api/health")
    def health():
        """서버 구동 상태와 현재 FAB 정보를 반환한다."""
        return ok(
            data={"server": SERVER_NAME, "fab": CURRENT_FAB, "status": "running"},
            data_type="dict",
            meta={"fab": CURRENT_FAB},
        )

    return app
