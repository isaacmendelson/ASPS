from dataclasses import dataclass, field
from typing import Any


@dataclass
class Document:
    text: str
    source: str
    path: str
    document_type: str | None = None
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass
class KnowledgeChunk:
    id: str
    text: str
    source: str
    path: str
    chunk_index: int
    document_type: str | None = None
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass
class RetrievalRequest:
    question: str
    agent_role: str | None = None
    project: str | None = None
    component: str | None = None
    document_types: list[str] | None = None
    tags: list[str] | None = None
    top_k: int = 5


@dataclass
class RetrievalResult:
    chunk: KnowledgeChunk
    score: float | None = None


@dataclass
class KnowledgeAnswer:
    question: str
    answer: str
    sources: list[KnowledgeChunk]