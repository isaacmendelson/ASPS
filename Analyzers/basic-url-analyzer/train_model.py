#!/usr/bin/env python3
"""
Train the ML scam classifier

Usage:
    python train_model.py                    # Train on sample data
    python train_model.py --data custom.json # Train on custom data
"""

import sys
import json
import argparse
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from core.ml_classifier import MLClassifier


def load_training_data(data_file: str):
    """Load training data from JSON file"""
    with open(data_file, 'r') as f:
        data = json.load(f)
    
    texts = [item['text'] for item in data]
    labels = [item['label'] for item in data]
    
    return texts, labels


def main():
    parser = argparse.ArgumentParser(description='Train ML scam classifier')
    parser.add_argument(
        '--data',
        default='training_data/sample_data.json',
        help='Path to training data JSON file'
    )
    parser.add_argument(
        '--model',
        default='models/scam_classifier.pkl',
        help='Path to save trained model'
    )
    
    args = parser.parse_args()
    
    print("=" * 60)
    print("ML SCAM CLASSIFIER TRAINING")
    print("=" * 60)
    print()
    
    # Load data
    print(f"Loading training data from: {args.data}")
    try:
        texts, labels = load_training_data(args.data)
        print(f"[OK] Loaded {len(texts)} samples")
        print(f"  - Scam samples: {sum(labels)}")
        print(f"  - Safe samples: {len(labels) - sum(labels)}")
        print()
    except Exception as e:
        print(f"[ERROR] Error loading data: {str(e)}")
        return 1
    
    # Initialize classifier
    print("Initializing ML classifier...")
    classifier = MLClassifier(model_path=args.model)
    print()
    
    # Train
    print("Training model...")
    result = classifier.train(texts, labels)
    print()
    
    if result['success']:
        print("[OK] Training successful!")
        print(f"  - Total samples: {result['samples']}")
        print(f"  - Train samples: {result['train_samples']} (80%)")
        print(f"  - Test samples: {result['test_samples']} (20%)")
        print()
        print("Model Performance:")
        print(f"  - Training accuracy: {result['train_accuracy']:.2%}")
        print(f"  - Test accuracy: {result['test_accuracy']:.2%}")
        print(f"  - CV accuracy: {result['cv_mean']:.2%} (+/- {result['cv_std'] * 2:.2%})")
        print()
        print("Cross-validation fold scores:")
        for i, score in enumerate(result['cv_scores'], 1):
            print(f"  Fold {i}: {score:.2%}")
        print()
        print(f"Model saved to: {result['model_path']}")
        print()

        # Test predictions
        print("Testing predictions:")
        print("-" * 60)
        
        test_cases = [
            "Bitcoin AI bot - earn $10k monthly guaranteed!",
            "Amazon sells products online",
            "Make money with our secret crypto algorithm!",
            "Python programming tutorial for beginners"
        ]
        
        for text in test_cases:
            pred = classifier.predict(text)
            status = "[SCAM]" if pred['is_scam'] else "[SAFE]"
            print(f"{status} ({pred['confidence']:.0%}): {text[:50]}...")
        
        print()
        print("=" * 60)
        print("[OK] Model ready to use!")
        print()
        print("You can now:")
        print("1. Run analysis: python analyze.py <url> --use-ml")
        print("2. Add more training data to improve accuracy")
        print("=" * 60)
        
        return 0
    else:
        print(f"[ERROR] Training failed: {result.get('error', 'Unknown error')}")
        return 1


if __name__ == '__main__':
    sys.exit(main())
