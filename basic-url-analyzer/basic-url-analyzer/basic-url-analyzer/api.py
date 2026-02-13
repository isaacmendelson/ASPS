"""
FastAPI server for URL Scam Analyzer
Run with: uvicorn api:app --host 0.0.0.0 --port 8000
"""

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, HttpUrl
from typing import Optional
import uvicorn

from core.analyzer import ScamAnalyzer

# Initialize FastAPI app
app = FastAPI(
    title="URL Scam Analyzer API",
    description="Analyze URLs for potential scams",
    version="1.0.0"
)

# CORS middleware - allow all origins for extension
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize analyzer (singleton)
analyzer = ScamAnalyzer(use_cache=True, use_ml=True, no_explain=True)


class AnalyzeRequest(BaseModel):
    url: str


class AnalyzeResponse(BaseModel):
    url: str
    risk_score: int
    risk_level: str
    is_scam: bool
    confidence: float
    domain_age_days: Optional[int] = None
    detected_language: Optional[str] = None
    category: Optional[str] = None
    red_flags: list = []
    error: Optional[str] = None


@app.get("/")
async def root():
    """Health check endpoint"""
    return {"status": "ok", "service": "URL Scam Analyzer"}


@app.get("/health")
async def health():
    """Health check for monitoring"""
    return {"status": "healthy"}


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze_url(request: AnalyzeRequest):
    """
    Analyze a URL for potential scams

    Returns risk assessment with score, level, and detected red flags.
    """
    try:
        result = analyzer.analyze_url(request.url)

        # Handle errors
        if result.get('error'):
            return AnalyzeResponse(
                url=request.url,
                risk_score=50,
                risk_level="UNKNOWN",
                is_scam=False,
                confidence=0.0,
                error=result.get('error')
            )

        # Extract data from result
        risk = result.get('risk_assessment', {})
        whois = result.get('whois', {})
        content = result.get('content_analysis', {})
        category = result.get('website_category', {})

        # Get red flags from detected patterns
        red_flags = content.get('detected_patterns', [])

        return AnalyzeResponse(
            url=result.get('url', request.url),
            risk_score=risk.get('risk_score', 50),
            risk_level=risk.get('risk_level', 'UNKNOWN'),
            is_scam=risk.get('is_scam', False),
            confidence=risk.get('confidence', 0.0),
            domain_age_days=whois.get('domain_age_days'),
            detected_language=content.get('detected_language'),
            category=category.get('category'),
            red_flags=red_flags
        )

    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/analyze")
async def analyze_url_get(url: str):
    """
    Analyze URL via GET request (for easy browser testing)
    """
    return await analyze_url(AnalyzeRequest(url=url))


if __name__ == "__main__":
    uvicorn.run(
        "api:app",
        host="0.0.0.0",
        port=8000,
        reload=False,
        workers=1
    )
