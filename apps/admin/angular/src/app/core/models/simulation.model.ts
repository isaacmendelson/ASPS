import { Key } from './key.model';
import { SimulationStatus } from './enums';

export interface SimulationStep {
  order: number;
  title: string;
  description: string;
  url?: string;
  expectedAction?: string;
}

export interface Simulation {
  key: Key;
  title: string;
  description?: string;
  status: SimulationStatus;
  steps: SimulationStep[];
  dateCreated: string;
  dateModified: string;
}

export interface CreateSimulationRequest {
  title: string;
  description?: string;
  steps: SimulationStep[];
}

export interface UpdateSimulationRequest {
  title: string;
  description?: string;
  status: SimulationStatus;
  steps: SimulationStep[];
}
