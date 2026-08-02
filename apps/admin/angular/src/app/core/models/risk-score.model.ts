export interface DataSource {
  name: string;
  value: number;
  weight: number;
}

export interface Signal {
  name: string;
  score: number;
  dataSources: DataSource[];
}

export interface DimensionScores {
  awareness: number;
  behavior: number;
  exposure: number;
}

export interface AxisScores {
  technical: number;
  social: number;
  physical: number;
}

export interface UserRiskScore {
  overallScore: number;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  dimensions: DimensionScores;
  axes: AxisScores;
  signals: Signal[];
  calculatedAt: string;
}
