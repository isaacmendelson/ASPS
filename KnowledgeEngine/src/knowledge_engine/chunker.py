from knowledge_engine.models import Document, KnowledgeChunk


class Chunker:
    def __init__(self, chunk_size: int = 800, overlap: int = 150):
        if chunk_size <= 0:
            raise ValueError("chunk_size must be greater than zero")

        if overlap < 0:
            raise ValueError("overlap cannot be negative")

        if overlap >= chunk_size:
            raise ValueError("overlap must be smaller than chunk_size")

        self.chunk_size = chunk_size
        self.overlap = overlap

    def chunk_document(self, document: Document) -> list[KnowledgeChunk]:
        text = self._normalize_text(document.text)
        chunks: list[KnowledgeChunk] = []

        start = 0
        chunk_index = 0

        while start < len(text):
            end = start + self.chunk_size
            chunk_text = text[start:end].strip()

            if len(chunk_text) > 100:
                chunks.append(
                    KnowledgeChunk(
                        id=self._build_chunk_id(document, chunk_index),
                        text=chunk_text,
                        source=document.source,
                        path=document.path,
                        chunk_index=chunk_index,
                        document_type=document.document_type,
                        metadata={
                            **document.metadata,
                            "chunk_size": self.chunk_size,
                            "overlap": self.overlap,
                        },
                    )
                )
                chunk_index += 1

            start += self.chunk_size - self.overlap

        return chunks

    def chunk_documents(self, documents: list[Document]) -> list[KnowledgeChunk]:
        chunks: list[KnowledgeChunk] = []

        for document in documents:
            chunks.extend(self.chunk_document(document))

        return chunks

    def _normalize_text(self, text: str) -> str:
        return " ".join(text.split())

    def _build_chunk_id(self, document: Document, chunk_index: int) -> str:
        safe_source = (
            document.source
            .replace("\\", "_")
            .replace("/", "_")
            .replace(" ", "_")
        )

        return f"{safe_source}-{chunk_index}"