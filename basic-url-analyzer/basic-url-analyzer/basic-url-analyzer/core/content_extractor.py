"""
HTML content extraction and analysis
"""

import re
from bs4 import BeautifulSoup
from typing import Dict, List
from pathlib import Path
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class ContentExtractor:
    """Extracts and analyzes content from HTML"""
    
    def __init__(self):
        """Initialize content extractor"""
        self.logger = setup_logger('content_extractor')
        
        # Common CTA button texts
        self.cta_patterns = [
            'buy', 'join', 'sign up', 'register', 'start', 'get started',
            'claim', 'download', 'order', 'subscribe', 'try', 'learn more',
            'act now', 'limited', 'offer', 'free'
        ]
        
        # URL shortener domains
        self.url_shorteners = [
            'bit.ly', 'tinyurl.com', 'goo.gl', 't.co', 'ow.ly', 
            'buff.ly', 'adf.ly', 'is.gd', 'shorte.st'
        ]
    
    def extract(self, html: str, url: str) -> Dict:
        """
        Extract content from HTML
        
        Args:
            html: HTML content
            url: Source URL
        
        Returns:
            Dictionary with extracted content
        """
        try:
            self.logger.info("Extracting content from HTML")
            
            soup = BeautifulSoup(html, 'lxml')
            
            # Remove only scripts and styles (keep nav/header/footer for content)
            for element in soup(['script', 'style']):
                element.decompose()
            
            # Extract title
            title = self._extract_title(soup)
            
            # Extract meta description
            meta_description = self._extract_meta_description(soup)
            
            # Extract headings
            headings = self._extract_headings(soup)
            
            # Extract body text
            body_text = self._extract_body_text(soup)
            
            # Extract CTAs
            cta_buttons = self._extract_ctas(soup)
            
            # Analyze links
            links_analysis = self._analyze_links(soup, url)
            
            # Analyze forms
            forms_analysis = self._analyze_forms(soup)
            
            # Count words
            word_count = len(body_text.split())
            
            result = {
                'success': True,
                'title': title,
                'meta_description': meta_description,
                'headings': headings,
                'body_text': body_text,
                'cta_buttons': cta_buttons,
                'cta_count': len(cta_buttons),
                'links': links_analysis,
                'forms': forms_analysis,
                'word_count': word_count,
                'error': ''
            }
            
            self.logger.info(f"Content extraction successful ({word_count} words)")
            return result
        
        except Exception as e:
            self.logger.error(f"Content extraction failed: {str(e)}")
            return {
                'success': False,
                'title': '',
                'meta_description': '',
                'headings': {},
                'body_text': '',
                'cta_buttons': [],
                'cta_count': 0,
                'links': {},
                'forms': {},
                'word_count': 0,
                'error': f"Extraction failed: {str(e)}"
            }
    
    def _extract_title(self, soup: BeautifulSoup) -> str:
        """Extract page title"""
        title_tag = soup.find('title')
        if title_tag:
            return title_tag.get_text(strip=True)
        
        # Try h1 as fallback
        h1 = soup.find('h1')
        if h1:
            return h1.get_text(strip=True)
        
        return ''
    
    def _extract_meta_description(self, soup: BeautifulSoup) -> str:
        """Extract meta description"""
        meta = soup.find('meta', attrs={'name': 'description'})
        if meta and meta.get('content'):
            return meta['content']
        return ''
    
    def _extract_headings(self, soup: BeautifulSoup) -> Dict[str, List[str]]:
        """Extract all headings"""
        headings = {}
        
        for level in ['h1', 'h2', 'h3']:
            tags = soup.find_all(level)
            if tags:
                headings[level] = [tag.get_text(strip=True) for tag in tags]
        
        return headings
    
    def _extract_body_text(self, soup: BeautifulSoup) -> str:
        """Extract main body text"""
        # Try to find main content area
        main = soup.find('main')
        article = soup.find('article')
        body = soup.find('body')

        # Prefer main/article if they have substantial content, otherwise use body
        main_content = None
        if main:
            main_text = main.get_text(separator=' ', strip=True)
            if len(main_text.split()) > 50:  # Only use main if it has decent content
                main_content = main
        if not main_content and article:
            article_text = article.get_text(separator=' ', strip=True)
            if len(article_text.split()) > 50:
                main_content = article
        if not main_content:
            main_content = body

        if main_content:
            text = main_content.get_text(separator=' ', strip=True)
            # Clean up multiple spaces
            text = re.sub(r'\s+', ' ', text)
            return text

        return ''
    
    def _extract_ctas(self, soup: BeautifulSoup) -> List[str]:
        """Extract call-to-action buttons"""
        ctas = []
        
        # Find buttons and links that look like CTAs
        for element in soup.find_all(['button', 'a']):
            text = element.get_text(strip=True).lower()
            
            # Check if matches CTA patterns
            for pattern in self.cta_patterns:
                if pattern in text:
                    ctas.append(element.get_text(strip=True))
                    break
        
        # Remove duplicates
        return list(set(ctas))
    
    def _analyze_links(self, soup: BeautifulSoup, source_url: str) -> Dict:
        """Analyze internal and external links"""
        from urllib.parse import urlparse
        
        source_domain = urlparse(source_url).netloc
        
        internal_links = 0
        external_links = 0
        suspicious_links = []
        
        for link in soup.find_all('a', href=True):
            href = link['href']
            
            # Skip anchors and javascript
            if href.startswith(('#', 'javascript:', 'mailto:')):
                continue
            
            # Parse link
            try:
                parsed = urlparse(href)
                link_domain = parsed.netloc
                
                # Check if URL shortener
                if any(shortener in link_domain for shortener in self.url_shorteners):
                    suspicious_links.append(href)
                
                # Count internal vs external
                if not link_domain or link_domain == source_domain:
                    internal_links += 1
                else:
                    external_links += 1
            
            except:
                continue
        
        return {
            'internal': internal_links,
            'external': external_links,
            'suspicious': suspicious_links,
            'has_suspicious': len(suspicious_links) > 0
        }
    
    def _analyze_forms(self, soup: BeautifulSoup) -> Dict:
        """Analyze forms on page"""
        forms = soup.find_all('form')
        
        form_types = []
        
        for form in forms:
            # Check input types
            inputs = form.find_all('input')
            
            for inp in inputs:
                input_type = inp.get('type', '').lower()
                input_name = inp.get('name', '').lower()
                
                # Check for payment-related fields
                if any(keyword in input_name for keyword in ['card', 'credit', 'cvv', 'payment']):
                    form_types.append('payment')
                    break
                
                # Check for email
                if input_type == 'email' or 'email' in input_name:
                    if 'email' not in form_types:
                        form_types.append('email')
        
        return {
            'count': len(forms),
            'types': list(set(form_types))
        }
