"""
ML-based scam classifier
Provides semantic analysis beyond regex patterns
"""

import json
import pickle
from pathlib import Path
from typing import Dict, List, Tuple, Optional
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline
from sklearn.model_selection import train_test_split, cross_val_score, StratifiedKFold
import numpy as np
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class MLClassifier:
    """Machine Learning scam classifier"""
    
    def __init__(self, model_path: Optional[str] = None):
        """
        Initialize ML classifier
        
        Args:
            model_path: Path to trained model file (optional)
        """
        self.logger = setup_logger('ml_classifier')
        
        if model_path is None:
            model_path = Path(__file__).parent.parent / 'models' / 'scam_classifier.pkl'
        
        self.model_path = Path(model_path)
        self.model = None
        self.is_trained = False
        
        # Try to load existing model
        if self.model_path.exists():
            self._load_model()
        else:
            # Create basic untrained model
            self._create_default_model()
    
    def predict(self, text: str) -> Dict:
        """
        Predict if text is scam
        
        Args:
            text: Text to analyze
        
        Returns:
            Dictionary with prediction and confidence
        """
        if not self.is_trained:
            self.logger.warning("ML model not trained - returning neutral score")
            return {
                'success': True,
                'is_scam': False,
                'confidence': 0.5,
                'score': 0.5,
                'note': 'Model not trained - train with training data for better results'
            }
        
        try:
            # Predict
            prediction = self.model.predict([text])[0]
            
            # Get probability scores
            probabilities = self.model.predict_proba([text])[0]
            
            # Score for scam class (class 1)
            scam_score = probabilities[1]
            
            result = {
                'success': True,
                'is_scam': bool(prediction),
                'confidence': float(scam_score),
                'score': float(scam_score),
                'note': 'ML prediction based on trained model'
            }
            
            self.logger.info(f"ML prediction: {'SCAM' if prediction else 'SAFE'} ({scam_score:.2f})")
            return result

        except Exception as e:
            self.logger.error(f"ML prediction failed: {str(e)}")
            return {
                'success': False,
                'is_scam': False,
                'confidence': 0.5,
                'score': 0.5,
                'note': f'Prediction error: {str(e)}'
            }

    def explain(self, text: str, top_n: int = 10) -> Dict:
        """
        Explain a prediction by showing contributing features

        Args:
            text: Text to analyze
            top_n: Number of top features to return for each class

        Returns:
            Dictionary with prediction and feature contributions
        """
        if not self.is_trained:
            return {
                'success': False,
                'error': 'Model not trained'
            }

        try:
            # Get prediction first
            prediction = self.model.predict([text])[0]
            probabilities = self.model.predict_proba([text])[0]
            scam_score = probabilities[1]

            # Get the TF-IDF vectorizer and classifier from pipeline
            tfidf = self.model.named_steps['tfidf']
            classifier = self.model.named_steps['classifier']

            # Transform input text to TF-IDF features
            tfidf_vector = tfidf.transform([text])

            # Get feature names
            feature_names = tfidf.get_feature_names_out()

            # Get coefficients - handle both LogisticRegression (coef_) and MultinomialNB (feature_log_prob_)
            if hasattr(classifier, 'coef_'):
                # LogisticRegression: coef_ gives direct feature weights
                coefficients = classifier.coef_[0]
            elif hasattr(classifier, 'feature_log_prob_'):
                # MultinomialNB: use log probability difference between classes
                # Positive = more likely scam, negative = more likely legitimate
                coefficients = classifier.feature_log_prob_[1] - classifier.feature_log_prob_[0]
            else:
                return {
                    'success': False,
                    'error': f'Unsupported classifier type: {type(classifier).__name__}'
                }

            # Get non-zero features from the input text
            non_zero_indices = tfidf_vector.nonzero()[1]

            # Calculate contribution of each feature (tfidf_value * coefficient)
            contributions = []
            for idx in non_zero_indices:
                feature = feature_names[idx]
                tfidf_value = tfidf_vector[0, idx]
                coef = coefficients[idx]
                contribution = tfidf_value * coef
                contributions.append({
                    'feature': feature,
                    'tfidf': float(tfidf_value),
                    'coefficient': float(coef),
                    'contribution': float(contribution)
                })

            # Sort by contribution (positive = scam, negative = legitimate)
            contributions.sort(key=lambda x: x['contribution'], reverse=True)

            # Get top scam indicators (positive contribution)
            scam_indicators = [c for c in contributions if c['contribution'] > 0][:top_n]

            # Get top legitimate signals (negative contribution)
            legit_signals = [c for c in contributions if c['contribution'] < 0]
            legit_signals.sort(key=lambda x: x['contribution'])  # Most negative first
            legit_signals = legit_signals[:top_n]

            return {
                'success': True,
                'is_scam': bool(prediction),
                'confidence': float(scam_score),
                'scam_indicators': scam_indicators,
                'legitimate_signals': legit_signals,
                'total_features_matched': len(contributions)
            }

        except Exception as e:
            self.logger.error(f"Explanation failed: {str(e)}")
            return {
                'success': False,
                'error': str(e)
            }
    
    def train(self, texts: List[str], labels: List[int]) -> Dict:
        """
        Train the model on labeled data with proper validation

        Args:
            texts: List of text samples
            labels: List of labels (0 = safe, 1 = scam)

        Returns:
            Training results including train/test accuracy and CV scores
        """
        try:
            self.logger.info(f"Training ML model on {len(texts)} samples")

            # Convert to numpy arrays
            texts = np.array(texts)
            labels = np.array(labels)

            # 80/20 stratified split
            X_train, X_test, y_train, y_test = train_test_split(
                texts, labels,
                test_size=0.2,
                random_state=42,
                stratify=labels
            )

            self.logger.info(f"Split: {len(X_train)} train, {len(X_test)} test samples")

            # 5-fold stratified cross-validation on training data
            cv = StratifiedKFold(n_splits=5, shuffle=True, random_state=42)
            cv_scores = cross_val_score(self.model, X_train, y_train, cv=cv, scoring='accuracy')

            self.logger.info(f"CV scores: {cv_scores}")
            self.logger.info(f"CV mean: {cv_scores.mean():.3f} (+/- {cv_scores.std() * 2:.3f})")

            # Train on full training set
            self.model.fit(X_train, y_train)
            self.is_trained = True

            # Evaluate on both sets
            train_accuracy = self.model.score(X_train, y_train)
            test_accuracy = self.model.score(X_test, y_test)

            # Save the model
            self._save_model()

            result = {
                'success': True,
                'samples': len(texts),
                'train_samples': len(X_train),
                'test_samples': len(X_test),
                'train_accuracy': float(train_accuracy),
                'test_accuracy': float(test_accuracy),
                'cv_scores': cv_scores.tolist(),
                'cv_mean': float(cv_scores.mean()),
                'cv_std': float(cv_scores.std()),
                'model_path': str(self.model_path)
            }

            self.logger.info(f"Training complete - Train: {train_accuracy:.2%}, Test: {test_accuracy:.2%}, CV: {cv_scores.mean():.2%}")
            return result

        except Exception as e:
            self.logger.error(f"Training failed: {str(e)}")
            return {
                'success': False,
                'error': str(e)
            }
    
    def add_training_samples(self, new_texts: List[str], new_labels: List[int]) -> Dict:
        """
        Add new training samples and retrain
        
        Args:
            new_texts: New text samples
            new_labels: New labels
        
        Returns:
            Retraining results
        """
        # This would need to store previous training data
        # For now, just retrain on new data
        return self.train(new_texts, new_labels)
    
    def _create_default_model(self):
        """Create optimized model pipeline with Logistic Regression"""
        self.model = Pipeline([
            ('tfidf', TfidfVectorizer(
                max_features=500,           # Reduced from 5000 to prevent overfitting
                ngram_range=(1, 2),          # Unigrams and bigrams (was 1,3)
                stop_words='english',
                lowercase=True
            )),
            ('classifier', LogisticRegression(
                C=1.0,                       # Regularization strength
                class_weight='balanced',    # Handle class imbalance
                solver='lbfgs',             # Good general-purpose solver
                max_iter=1000,              # Prevent ConvergenceWarning
                random_state=42             # Reproducibility
            ))
        ])
        self.is_trained = False
        self.logger.info("Created default ML model with Logistic Regression (untrained)")
    
    def _load_model(self):
        """Load trained model from file"""
        try:
            with open(self.model_path, 'rb') as f:
                self.model = pickle.load(f)
            
            self.is_trained = True
            self.logger.info(f"Loaded trained model from {self.model_path}")
        
        except Exception as e:
            self.logger.warning(f"Failed to load model: {str(e)}")
            self._create_default_model()
    
    def _save_model(self):
        """Save trained model to file"""
        try:
            # Create models directory if doesn't exist
            self.model_path.parent.mkdir(parents=True, exist_ok=True)
            
            with open(self.model_path, 'wb') as f:
                pickle.dump(self.model, f)
            
            self.logger.info(f"Model saved to {self.model_path}")
        
        except Exception as e:
            self.logger.error(f"Failed to save model: {str(e)}")
