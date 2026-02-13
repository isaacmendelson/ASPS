#!/usr/bin/env python3
"""
Import known phishing URLs from CSV file into ASPSBackend database

CSV Format:
-----------
Option 1: Single column (url)
url
http://phishing-site.com
https://scam-site.com

Option 2: Two columns (url, source)
url,source
http://phishing-site.com,PhishTank
https://scam-site.com,OpenPhish

Usage:
------
python import-phishing-urls.py phishing_urls.csv

Requirements:
-------------
pip install mysql-connector-python
"""

import csv
import sys
import mysql.connector
from datetime import datetime
from urllib.parse import urlparse

def get_domain_from_url(url):
    """Extract domain from URL"""
    try:
        if not url:
            return ''
        
        # Add scheme if missing
        if '://' not in url:
            url = 'http://' + url
        
        parsed = urlparse(url)
        return parsed.netloc.lower()
    except:
        return ''

def import_csv(csv_file, host='localhost', port=3306, user='root', password='zappa22', database='ASPSBackend2DB'):
    """Import phishing URLs from CSV file"""
    
    print("=" * 70)
    print("Phishing URL Import Utility")
    print("=" * 70)
    print(f"\nCSV File: {csv_file}")
    print(f"Database: {database}")
    print(f"Host: {host}:{port}")
    print()
    
    # Connect to database
    print("Connecting to database...")
    try:
        conn = mysql.connector.connect(
            host=host,
            port=port,
            user=user,
            password=password,
            database=database
        )
        cursor = conn.cursor()
        print("✓ Connected successfully\n")
    except Exception as e:
        print(f"✗ Failed to connect: {e}")
        return
    
    # Read CSV file
    print(f"Reading CSV file: {csv_file}")
    try:
        with open(csv_file, 'r', encoding='utf-8') as f:
            # Try to detect delimiter
            sample = f.read(1024)
            f.seek(0)
            
            # Count commas vs tabs
            comma_count = sample.count(',')
            tab_count = sample.count('\t')
            delimiter = ',' if comma_count > tab_count else '\t'
            
            reader = csv.DictReader(f, delimiter=delimiter)
            
            # Get headers
            headers = reader.fieldnames
            print(f"✓ CSV Headers: {headers}\n")
            
            # Check if we have required columns
            has_url = 'url' in [h.lower() for h in headers] if headers else False
            has_source = 'source' in [h.lower() for h in headers] if headers else False
            
            if not has_url:
                print("✗ CSV must have 'url' column")
                return
            
            print("Processing URLs...")
            print("-" * 70)
            
            imported = 0
            skipped = 0
            errors = 0
            
            for row_num, row in enumerate(reader, start=2):
                try:
                    # Get URL (try different case variations)
                    url = row.get('url') or row.get('Url') or row.get('URL') or ''
                    url = url.strip()
                    
                    if not url:
                        skipped += 1
                        continue
                    
                    # Get source if available
                    source = ''
                    if has_source:
                        source = row.get('source') or row.get('Source') or ''
                        source = source.strip()[:100]  # Max 100 chars
                    
                    # Extract domain
                    domain = get_domain_from_url(url)
                    if not domain:
                        print(f"  Row {row_num}: Invalid URL - {url[:50]}")
                        skipped += 1
                        continue
                    
                    # Check if URL already exists
                    cursor.execute(
                        "SELECT `Key` FROM KnownPhishingWebsites WHERE Url = %s AND DateDeleted IS NULL",
                        (url,)
                    )
                    exists = cursor.fetchone()
                    
                    if exists:
                        skipped += 1
                        continue
                    
                    # Insert into database
                    cursor.execute(
                        """
                        INSERT INTO KnownPhishingWebsites 
                        (Url, Domain, DateCreated, Source) 
                        VALUES (%s, %s, %s, %s)
                        """,
                        (url, domain, datetime.utcnow(), source or None)
                    )
                    
                    imported += 1
                    
                    # Show progress
                    if imported % 100 == 0:
                        print(f"  Imported: {imported}")
                        conn.commit()
                
                except Exception as e:
                    errors += 1
                    print(f"  Row {row_num}: Error - {e}")
            
            # Commit final batch
            conn.commit()
            
            print("-" * 70)
            print(f"\n✓ Import completed!")
            print(f"  Imported: {imported}")
            print(f"  Skipped:  {skipped}")
            print(f"  Errors:   {errors}")
            
            # Show final count
            cursor.execute("SELECT COUNT(*) FROM KnownPhishingWebsites WHERE DateDeleted IS NULL")
            total = cursor.fetchone()[0]
            print(f"\n  Total active phishing URLs in database: {total}")
            
    except FileNotFoundError:
        print(f"✗ File not found: {csv_file}")
    except Exception as e:
        print(f"✗ Error reading CSV: {e}")
    finally:
        cursor.close()
        conn.close()
        print("\n" + "=" * 70)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python import-phishing-urls.py <csv_file>")
        print("\nExample CSV format:")
        print("  url,source")
        print("  http://phishing-site.com,PhishTank")
        print("  https://scam-site.com,OpenPhish")
        sys.exit(1)
    
    csv_file = sys.argv[1]
    import_csv(csv_file)
