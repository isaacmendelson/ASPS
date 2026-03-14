#!/usr/bin/env python3
"""
Get ASPS Backend version information from the API
Usage: python get_version.py [--base-url URL]
"""

import argparse
import json
import sys
import urllib.request
import urllib.error


def get_version(base_url: str = "http://localhost:5000") -> dict:
    """
    Retrieve version information from the ASPS Backend API
    
    Args:
        base_url: Base URL of the API (default: http://localhost:5000)
        
    Returns:
        Dictionary with version information
        
    Raises:
        Exception: If the API request fails
    """
    url = f"{base_url.rstrip('/')}/api/System/version"
    
    try:
        with urllib.request.urlopen(url, timeout=10) as response:
            if response.status != 200:
                raise Exception(f"HTTP {response.status}: {response.reason}")
            
            data = json.loads(response.read().decode('utf-8'))
            return data
            
    except urllib.error.URLError as e:
        raise Exception(f"Failed to connect to {url}: {e.reason}")
    except json.JSONDecodeError as e:
        raise Exception(f"Invalid JSON response: {e}")


def main():
    parser = argparse.ArgumentParser(description="Get ASPS Backend version")
    parser.add_argument(
        "--base-url",
        default="http://localhost:5000",
        help="Base URL of the API (default: http://localhost:5000)"
    )
    parser.add_argument(
        "--format",
        choices=["json", "text"],
        default="text",
        help="Output format (default: text)"
    )
    
    args = parser.parse_args()
    
    try:
        version_info = get_version(args.base_url)
        
        if args.format == "json":
            print(json.dumps(version_info, indent=2))
        else:
            print(f"Version: {version_info.get('version', 'N/A')}")
            print(f"Git Commit: {version_info.get('gitCommitId', 'N/A')}")
            print(f"Is Prerelease: {version_info.get('isPrerelease', 'N/A')}")
            print(f"Is Public Release: {version_info.get('isPublicRelease', 'N/A')}")
            print(f"Build Date: {version_info.get('buildDate', 'N/A')}")
        
        return 0
        
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
