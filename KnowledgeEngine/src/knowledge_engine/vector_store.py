from pathlib import Path
from typing import Any

import chromadb

from knowledge_engine.models import KnowledgeChunk, RetrievalResult


class VectorStore:
    def __init__(
        self,
        db_path: str | Path,
        collection_name: str = "asps_knowledge",
    ):
        self.db_path = Path(db_path)
        self.collection_name = collection_name

        self.client = chromadb.PersistentClient(path=str(self.db_path))
        self.collection = self.client.get_or_create_collection(
            name=self.collection_name
        )

    def upsert_chunks(self, chunks: list[KnowledgeChunk]) -> None:
        if not chunks:
            return

        ids = [chunk.id for chunk in chunks]
        documents = [chunk.text for chunk in chunks]
        metadatas = [self._metadata_from_chunk(chunk) for chunk in chunks]

        self.collection.upsert(
            ids=ids,
            documents=documents,
            metadatas=metadatas,
        )

    def search(
        self,
        query: str,
        top_k: int = 5,
        filters: dict[str, Any] | None = None,
    ) -> list[RetrievalResult]:
        where = filters if filters else None

        results = self.collection.query(
            query_texts=[query],
            n_results=top_k,
            where=where,
        )

        documents = results.get("documents", [[]])[0]
        metadatas = results.get("metadatas", [[]])[0]
        distances = results.get("distances", [[]])[0]

        output: list[RetrievalResult] = []

        for i, text in enumerate(documents):
            metadata = metadatas[i] or {}
            distance = distances[i] if i < len(distances) else None

            chunk = KnowledgeChunk(
                id=metadata.get("id", f"result-{i}"),
                text=text,
                source=metadata.get("source", ""),
                path=metadata.get("path", ""),
                chunk_index=int(metadata.get("chunk_index", 0)),
                document_type=metadata.get("document_type"),
                metadata=metadata,
            )

            output.append(
                RetrievalResult(
                    chunk=chunk,
                    score=distance,
                )
            )

        return output

    def count(self) -> int:
        return self.collection.count()

    def delete_collection(self) -> None:
        self.client.delete_collection(self.collection_name)
        self.collection = self.client.get_or_create_collection(
            name=self.collection_name
        )

    def _metadata_from_chunk(self, chunk: KnowledgeChunk) -> dict[str, Any]:
        metadata = {
            **chunk.metadata,
            "id": chunk.id,
            "source": chunk.source,
            "path": chunk.path,
            "chunk_index": chunk.chunk_index,
            "document_type": chunk.document_type or "General",
        }

        return self._sanitize_metadata(metadata)

    def _sanitize_metadata(self, metadata: dict[str, Any]) -> dict[str, Any]:
        sanitized: dict[str, Any] = {}

        for key, value in metadata.items():
            if value is None:
                continue

            if isinstance(value, (str, int, float, bool)):
                sanitized[key] = value
            else:
                sanitized[key] = str(value)

        return sanitized