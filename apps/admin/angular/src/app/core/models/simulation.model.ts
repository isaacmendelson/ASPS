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
  /** keyField is the string used in URL paths: key.type/key.value. */
  keyField: string;
  name: string;
  description?: string;
  status: SimulationStatus;
  steps: SimulationStep[];
  dateCreated: string;
  creatorKeyField?: string;
}

export interface CreateSimulationRequest {
  name: string;
  description?: string;
  creatorKeyField?: string;
  steps: SimulationStep[];
}

export interface UpdateSimulationRequest {
  name: string;
  description?: string;
  steps: SimulationStep[];
}
