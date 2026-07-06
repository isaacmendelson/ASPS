import os
from abc import ABC, abstractmethod

from anthropic import Anthropic
from dotenv import load_dotenv


class LlmProvider(ABC):
    @abstractmethod
    def generate(self, prompt: str) -> str:
        pass


class AnthropicProvider(LlmProvider):
    def __init__(
        self,
        model: str = "claude-sonnet-4-6",
        max_tokens: int = 1200,
    ):
        load_dotenv()

        api_key = os.getenv("ANTHROPIC_API_KEY")
        if not api_key:
            raise RuntimeError(
                "Missing ANTHROPIC_API_KEY. "
                "Create a .env file or set the environment variable."
            )

        self.client = Anthropic(api_key=api_key)
        self.model = model
        self.max_tokens = max_tokens

    def generate(self, prompt: str) -> str:
        response = self.client.messages.create(
            model=self.model,
            max_tokens=self.max_tokens,
            messages=[
                {
                    "role": "user",
                    "content": prompt,
                }
            ],
        )

        return response.content[0].text


class OllamaProvider(LlmProvider):
    def __init__(
        self,
        model: str = "llama3.2",
        base_url: str = "http://localhost:11434",
    ):
        try:
            import ollama as _ollama
            self._ollama = _ollama
        except ImportError:
            raise RuntimeError(
                "ollama package not installed. Run: pip install ollama"
            )
        self.model = model
        self.base_url = base_url

    def generate(self, prompt: str) -> str:
        response = self._ollama.chat(
            model=self.model,
            messages=[{"role": "user", "content": prompt}],
        )
        return response.message.content