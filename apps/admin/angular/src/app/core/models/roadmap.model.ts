import { Key } from './key.model';
import { Priority } from './enums';

export interface RoadmapItem {
  order: number;
  title: string;
  description?: string;
  isCompleted: boolean;
}

export interface Roadmap {
  key: Key;
  title: string;
  description?: string;
  priority: Priority;
  items: RoadmapItem[];
  dateCreated: string;
  dateModified: string;
}
