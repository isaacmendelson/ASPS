import os
from pathlib import Path

from dotenv import load_dotenv

load_dotenv()

PROJECT_ROOT = Path(__file__).resolve().parents[2]

DB_PATH = PROJECT_ROOT / "db"

ASPS_DOCS = Path(
    os.getenv(
        "ASPS_DOCS",
        r"C:\Jobs\ASPS\GitHub\Software\docs"
    )
)

ASPS_CLAUDE = Path(
    os.getenv(
        "ASPS_CLAUDE",
        r"C:\Jobs\ASPS\GitHub\Software\.claude"
    )
)

TOP_K = int(os.getenv("TOP_K", "5"))

COLLECTION_NAME = os.getenv(
    "COLLECTION_NAME",
    "asps_knowledge"
)

KNOWLEDGE_SOURCES = [
    ASPS_DOCS,
    ASPS_CLAUDE,
]
