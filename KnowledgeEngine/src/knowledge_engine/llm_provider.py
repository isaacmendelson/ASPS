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