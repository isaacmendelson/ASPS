#!/usr/bin/env python3
"""Add legitimate website samples to training data to reduce false positives."""

import json
from datetime import date
from pathlib import Path

TODAY = date.today().isoformat()


def create_legitimate_samples():
    """Create legitimate website samples (label=0) for social networks, tech platforms, etc."""
    samples = []

    # Social Network Samples
    social_samples = [
        {
            "id": "legit_social_0001",
            "text": """Welcome to LinkedIn - the world's largest professional network.
            Sign in to stay updated on your professional world. New to LinkedIn? Join now.
            Connect with colleagues, classmates, and friends. Build your professional network.
            Find jobs, people, companies, and more. LinkedIn is the world's largest business network,
            helping professionals discover inside connections. Over 1 billion members worldwide.
            Post your profile, find jobs, connect with people. Privacy Policy. User Agreement.
            Copyright 2024 LinkedIn Corporation.""",
            "label": 0,
            "category": "social_network_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "social_network"}
        },
        {
            "id": "legit_social_0002",
            "text": """Facebook helps you connect and share with the people in your life.
            Log In or Sign Up. Create a Page for a celebrity, band or business.
            Connect with friends and the world around you on Facebook. See photos and updates
            from friends. Find events, games, Pages, and more. Privacy. Terms. Advertising.
            Meta 2024. People use Facebook to keep up with friends, family, and communities.""",
            "label": 0,
            "category": "social_network_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "social_network"}
        },
        {
            "id": "legit_social_0003",
            "text": """X is what's happening in the world and what people are talking about right now.
            Sign up. Log in. Join the conversation. Follow your interests. Instant updates about
            what matters to you. See what's happening. Find people you know. Post, like, reply.
            Discover trending topics. Privacy Policy. Terms of Service. Cookie Policy.
            Copyright 2024 X Corp.""",
            "label": 0,
            "category": "social_network_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "social_network"}
        },
        {
            "id": "legit_social_0004",
            "text": """Instagram - Create an account or log in. Share photos and videos with friends,
            family, and the world. Discover inspiring creators. Follow your favorite accounts.
            Post stories and reels. Explore trending content. Direct messages.
            From Meta. Terms of Use. Privacy Policy. About. Help. Press.""",
            "label": 0,
            "category": "social_network_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "social_network"}
        },
        {
            "id": "legit_social_0005",
            "text": """TikTok - Make Your Day. Watch, create, and discover short videos.
            Log in. Sign up. Download the app. Trending videos and sounds.
            Creator tools. Effects and filters. For You page. Following feed.
            Community Guidelines. Safety Center. Privacy Policy. Terms of Service.
            Copyright 2024 TikTok.""",
            "label": 0,
            "category": "social_network_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "social_network"}
        },
    ]
    samples.extend(social_samples)

    # Tech Company Samples
    tech_samples = [
        {
            "id": "legit_tech_0001",
            "text": """Google - Search the world's information, including webpages, images, videos and more.
            Google has many special features to help you find exactly what you're looking for.
            Gmail. Images. Sign in. Advanced search. Advertising. Business Solutions.
            About Google. Privacy. Terms. Settings.""",
            "label": 0,
            "category": "tech_company_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "search_engine"}
        },
        {
            "id": "legit_tech_0002",
            "text": """Microsoft - Cloud Computing Services. AI Solutions. Business Applications.
            Microsoft 365. Azure. Windows. Xbox. Surface. Microsoft Teams.
            Empowering every person and organization on the planet to achieve more.
            Products and services. Support. Security. Privacy. Terms of use.""",
            "label": 0,
            "category": "tech_company_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "technology"}
        },
        {
            "id": "legit_tech_0003",
            "text": """Apple - iPhone, iPad, Mac, Apple Watch, AirPods. Explore the innovative world of Apple
            and shop everything iPhone, iPad, Apple Watch, Mac, and Apple TV. Store. Mac. iPad. iPhone.
            Watch. Vision. AirPods. TV & Home. Support. Copyright 2024 Apple Inc. All rights reserved.
            Privacy Policy. Terms of Use.""",
            "label": 0,
            "category": "tech_company_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "technology"}
        },
        {
            "id": "legit_tech_0004",
            "text": """Amazon - Online Shopping for Electronics, Apparel, Computers, Books, DVDs & more.
            Free shipping on millions of items. Get the best of Shopping and Entertainment with Prime.
            Today's Deals. Customer Service. Registry. Gift Cards. Sell.
            Back to top. Conditions of Use. Privacy Notice. Your Ads Privacy Choices.""",
            "label": 0,
            "category": "ecommerce_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ecommerce"}
        },
        {
            "id": "legit_tech_0005",
            "text": """GitHub - Where the world builds software. Millions of developers and companies build,
            ship, and maintain their software on GitHub. Sign up for free. Enterprise. Features.
            Copilot. Security. Actions. Code review. Issues. Pull requests. Discussions.
            Privacy. Terms. Security. Status.""",
            "label": 0,
            "category": "developer_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "developer_tools"}
        },
        {
            "id": "legit_tech_0006",
            "text": """Stack Overflow - Where Developers Learn, Share, & Build Careers.
            Trusted by over 100 million developers. Find the best answer to your technical question.
            Log in. Sign up. Products. For Teams. Company. Questions. Tags. Users. Companies.
            Collectives. Explore. Stack Overflow for Teams. Privacy Policy. Terms of Service.""",
            "label": 0,
            "category": "developer_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "developer_community"}
        },
    ]
    samples.extend(tech_samples)

    # AI/ML Platform Samples
    ai_samples = [
        {
            "id": "legit_ai_0001",
            "text": """Ollama - Get up and running with large language models locally.
            Run Llama 3, Phi 3, Mistral, Gemma 2, and other models.
            Download. Models. Blog. GitHub. Discord.
            Customize and create your own models. Library of pre-built models.
            Privacy focused - runs on your machine.""",
            "label": 0,
            "category": "ai_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_tools"}
        },
        {
            "id": "legit_ai_0002",
            "text": """OpenAI - Creating safe AGI that benefits all of humanity.
            ChatGPT. GPT-4. DALL-E. API. Safety. Research. Company. Careers.
            Log in. Sign up. Try ChatGPT. API documentation. Developer platform.
            Terms of use. Privacy policy. Brand guidelines.""",
            "label": 0,
            "category": "ai_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_company"}
        },
        {
            "id": "legit_ai_0003",
            "text": """Anthropic - AI safety and research company. Building reliable, interpretable,
            and steerable AI systems. Claude AI assistant. Constitutional AI.
            Research. Products. Company. Careers. News.
            Terms of Service. Privacy Policy. Responsible Disclosure.""",
            "label": 0,
            "category": "ai_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_company"}
        },
        {
            "id": "legit_ai_0004",
            "text": """Hugging Face - The AI community building the future.
            Models. Datasets. Spaces. Docs. Solutions. Pricing.
            Collaborate on machine learning. Host models and datasets.
            Open source community. Transformers library.
            Terms of Service. Privacy Policy.""",
            "label": 0,
            "category": "ai_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_community"}
        },
        {
            "id": "legit_ai_0005",
            "text": """Claude - Talk to Claude, an AI assistant from Anthropic.
            Start a new chat. Sign in. Sign up. Continue with Google.
            Claude can help with analysis, writing, math, coding, and more.
            Safe and helpful AI assistant. Research-backed alignment.
            Terms of Service. Privacy Policy. Acceptable Use Policy.""",
            "label": 0,
            "category": "ai_assistant_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_assistant"}
        },
        {
            "id": "legit_ai_0006",
            "text": """ChatGPT - Get instant answers, find creative inspiration.
            Log in. Sign up. Try ChatGPT. ChatGPT Plus. Enterprise.
            Ask anything. Get answers. Creative writing. Code assistance.
            Built by OpenAI. Terms of use. Privacy policy.""",
            "label": 0,
            "category": "ai_assistant_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_assistant"}
        },
        {
            "id": "legit_ai_0007",
            "text": """Google Gemini - Your AI assistant powered by Google.
            Sign in to continue. Create account. Chat with Gemini.
            Get help with writing, planning, learning, and more.
            Powered by Google DeepMind. Privacy. Terms.""",
            "label": 0,
            "category": "ai_assistant_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_assistant"}
        },
        {
            "id": "legit_ai_0008",
            "text": """Perplexity AI - Ask anything. Get answers with sources.
            Sign in. Sign up. Search the web with AI.
            Accurate answers with citations. Research assistant.
            Pro. Enterprise. API. Privacy Policy. Terms of Service.""",
            "label": 0,
            "category": "ai_assistant_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ai_search"}
        },
    ]
    samples.extend(ai_samples)

    # Domain Registrar Samples
    registrar_samples = [
        {
            "id": "legit_registrar_0001",
            "text": """Namecheap - Domain Names, Web Hosting & SSL Certificates.
            Search for your next domain. Domain registration from $5.98/year.
            Web Hosting. Private Email. SSL Certificates. VPN.
            Sign up. Log in. Knowledge Base. Support.
            Terms of Service. Privacy Policy. ICANN Registrar.""",
            "label": 0,
            "category": "domain_registrar_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "domain_registrar"}
        },
        {
            "id": "legit_registrar_0002",
            "text": """GoDaddy - Domain Names, Websites, Hosting & Online Marketing Tools.
            Find your perfect domain. Website builder. Web hosting. Email.
            Sign In. Create Account. Domains. Websites. Hosting. Security.
            24/7 Support. ICANN Accredited Registrar.
            Terms of Service. Privacy Policy. Legal.""",
            "label": 0,
            "category": "domain_registrar_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "domain_registrar"}
        },
        {
            "id": "legit_registrar_0003",
            "text": """Google Domains - Find a domain, create a site, and get custom email.
            Search for a domain. Build a website. Get email with your domain.
            Sign in with Google. Simple pricing. Privacy protection included.
            Help. Terms of Service. Privacy Policy.""",
            "label": 0,
            "category": "domain_registrar_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "domain_registrar"}
        },
        {
            "id": "legit_registrar_0004",
            "text": """Cloudflare - The Web Performance & Security Company.
            Protect and accelerate your websites and apps.
            Log In. Sign Up. Products. Solutions. Developers.
            CDN. DDoS Protection. DNS. SSL/TLS. Zero Trust.
            Terms of Use. Privacy Policy. Trust & Safety.""",
            "label": 0,
            "category": "web_infrastructure_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "cdn_security"}
        },
        {
            "id": "legit_registrar_0005",
            "text": """Porkbun - Domain Name Registrar - An oddly satisfying experience.
            Domain search. Low prices. Free WHOIS privacy.
            Sign in. Create account. Domains. Hosting. Email.
            Knowledge Base. Support. ICANN Accredited.
            Terms of Service. Privacy Policy.""",
            "label": 0,
            "category": "domain_registrar_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "domain_registrar"}
        },
    ]
    samples.extend(registrar_samples)

    # News/Media Samples
    news_samples = [
        {
            "id": "legit_news_0001",
            "text": """BBC News - Trusted World and UK News.
            Breaking news, sport, TV, radio and a whole lot more.
            The BBC informs, educates and entertains. Home. UK. World. Business.
            Politics. Tech. Science. Health. Entertainment. Video.
            Terms of Use. About the BBC. Privacy Policy.""",
            "label": 0,
            "category": "news_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "news"}
        },
        {
            "id": "legit_news_0002",
            "text": """The New York Times - Breaking News, US News, World News and Videos.
            Live news, investigations, opinion, photos and video by the journalists
            of The New York Times from more than 150 countries around the world.
            Subscribe. Log In. US. World. Business. Arts. Opinion. Tech.
            Privacy Policy. Terms of Service. Contact Us.""",
            "label": 0,
            "category": "news_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "news"}
        },
        {
            "id": "legit_news_0003",
            "text": """Wikipedia - The Free Encyclopedia.
            Welcome to Wikipedia, the free encyclopedia that anyone can edit.
            Featured article. Did you know. In the news. On this day.
            Search Wikipedia. Create account. Log in. Main page. Contents.
            Current events. Random article. About Wikipedia. Contact us.
            Privacy policy. Terms of Use. Creative Commons license.""",
            "label": 0,
            "category": "reference_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "encyclopedia"}
        },
    ]
    samples.extend(news_samples)

    # E-commerce/Business Samples
    business_samples = [
        {
            "id": "legit_business_0001",
            "text": """eBay - Electronics, Cars, Fashion, Collectibles & More.
            Buy and sell electronics, cars, fashion apparel, collectibles, sporting goods,
            digital cameras, baby items, coupons. Shop. Sell. My eBay. Customer Service.
            Site Map. Help & Contact. Copyright 1995-2024 eBay Inc.
            User Agreement. Privacy. Payments Terms of Use.""",
            "label": 0,
            "category": "ecommerce_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "marketplace"}
        },
        {
            "id": "legit_business_0002",
            "text": """Shopify - Start and grow your e-commerce business.
            Build an online store with Shopify's e-commerce software.
            Sell online, in-store, and everywhere in between.
            Free trial. Pricing. Solutions. Resources. Enterprise.
            Terms of Service. Privacy Policy. Sitemap.""",
            "label": 0,
            "category": "business_platform_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "ecommerce_platform"}
        },
        {
            "id": "legit_business_0003",
            "text": """Stripe - Financial Infrastructure for the Internet.
            Millions of companies use Stripe to accept payments, send payouts,
            and manage their businesses online. Products. Solutions. Developers.
            Resources. Pricing. Documentation. API reference. Support.
            Privacy & Terms. Sitemap.""",
            "label": 0,
            "category": "fintech_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "payment_processor"}
        },
        {
            "id": "legit_business_0004",
            "text": """PayPal - The safer, easier way to pay online.
            Link your credit cards, bank accounts, and PayPal balance.
            Send money. Shop. Business. Help. Sign Up. Log In.
            Personal. Business. Fees. Security. Apps.
            User Agreement. Privacy. Copyright.""",
            "label": 0,
            "category": "fintech_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "payment_processor"}
        },
    ]
    samples.extend(business_samples)

    # Cloud/SaaS Samples
    saas_samples = [
        {
            "id": "legit_saas_0001",
            "text": """Slack - Where Work Happens.
            Slack is the collaboration hub that brings the right people, information,
            and tools together to get work done. Sign in. Get Started.
            Features. Solutions. Enterprise. Resources. Pricing.
            Privacy Policy. Terms of Service. Cookie Preferences.""",
            "label": 0,
            "category": "saas_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "communication"}
        },
        {
            "id": "legit_saas_0002",
            "text": """Zoom - Video Conferencing, Cloud Phone, Webinars.
            Zoom is the leader in modern enterprise video communications.
            An easy, reliable cloud platform for video and audio conferencing.
            Join a Meeting. Host a Meeting. Sign In. Sign Up Free.
            Plans & Pricing. Support. Privacy. Trust Center.""",
            "label": 0,
            "category": "saas_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "video_conferencing"}
        },
        {
            "id": "legit_saas_0003",
            "text": """Notion - Your connected workspace for wiki, docs & projects.
            A new tool that blends your everyday work apps into one.
            It's the all-in-one workspace for you and your team.
            Product. Teams. Individuals. Download. Pricing.
            Terms & Privacy. Security. Cookie Settings.""",
            "label": 0,
            "category": "saas_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "productivity"}
        },
        {
            "id": "legit_saas_0004",
            "text": """Dropbox - Secure File Sharing and Storage.
            Dropbox helps you keep all your photos, docs, and videos safe
            and automatically backed up. Sign up for free. Log in.
            Products. Solutions. Enterprise. Pricing. Support.
            Privacy & Terms. Cookie Policy. Sitemap.""",
            "label": 0,
            "category": "saas_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "cloud_storage"}
        },
    ]
    samples.extend(saas_samples)

    # More social/professional samples with login pages
    login_page_samples = [
        {
            "id": "legit_login_0001",
            "text": """Sign in to continue to LinkedIn. Email or phone. Password.
            Show password. Forgot password? Sign in. Or. Sign in with Apple.
            Sign in with Google. New to LinkedIn? Join now.
            User Agreement. Privacy Policy. Cookie Policy. Copyright Policy.
            Brand Policy. Guest Controls. Community Guidelines. Language.""",
            "label": 0,
            "category": "login_page_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "login_page"}
        },
        {
            "id": "legit_login_0002",
            "text": """Sign in to your Microsoft account. Sign in. Email, phone, or Skype.
            No account? Create one! Can't access your account?
            Sign-in options. Terms of use. Privacy & cookies.
            Microsoft 2024. Learn more about Microsoft accounts.""",
            "label": 0,
            "category": "login_page_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "login_page"}
        },
        {
            "id": "legit_login_0003",
            "text": """Sign in with your Google Account. Email or phone.
            Forgot email? Not your computer? Use Guest mode to sign in privately.
            Learn more. Create account. Next. Privacy. Terms. Help.""",
            "label": 0,
            "category": "login_page_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "login_page"}
        },
        {
            "id": "legit_login_0004",
            "text": """Log in to Facebook. Email address or phone number. Password.
            Log In. Forgot password? Create new account.
            You may also log in with. Continue with Apple. Continue with Google.
            Meta. About. Help. Privacy. Terms.""",
            "label": 0,
            "category": "login_page_legitimate",
            "metadata": {"source": "synthetic_legit", "date_added": TODAY, "confidence": "high", "verified_by": "auto", "site_type": "login_page"}
        },
    ]
    samples.extend(login_page_samples)

    return samples


def main():
    """Main function to add legitimate samples to training data."""
    training_data_path = Path(__file__).parent.parent / 'training_data' / 'sample_data.json'

    # Load existing data
    with open(training_data_path, 'r') as f:
        data = json.load(f)

    print(f"Loaded {len(data)} existing samples")

    # Count existing labels
    scam_count = sum(1 for s in data if s.get('label') == 1)
    legit_count = sum(1 for s in data if s.get('label') == 0)
    print(f"Current distribution: {scam_count} scam, {legit_count} legitimate")

    # Create legitimate samples
    samples = create_legitimate_samples()
    print(f"Created {len(samples)} legitimate samples")

    # Check for duplicates
    existing_ids = {s.get('id') for s in data}
    new_samples = [s for s in samples if s.get('id') not in existing_ids]

    if len(new_samples) < len(samples):
        print(f"Skipping {len(samples) - len(new_samples)} duplicate samples")

    # Add to data
    data.extend(new_samples)

    # Save
    with open(training_data_path, 'w') as f:
        json.dump(data, f, indent=2)

    # Count new distribution
    scam_count = sum(1 for s in data if s.get('label') == 1)
    legit_count = sum(1 for s in data if s.get('label') == 0)
    print(f"New distribution: {scam_count} scam, {legit_count} legitimate")
    print(f"Total samples now: {len(data)}")


if __name__ == "__main__":
    main()
