// Mirrors Common.Enums.Enumerations in the .NET backend.
// String union types match StringEnumConverter serialization.

export type DeviceType = 'Unknown' | 'PersonalComputer' | 'MobilePhone' | 'Other';

export type MonitoringStatus = 'Disabled' | 'Enabled';

export type OperatingSystemType = 'Unknown' | 'Windows' | 'Linux' | 'MacOS' | 'Android' | 'iOS';

export type UserRole = 'Unknown' | 'Self' | 'Guardian' | 'Other';

export type CautionLevel = 'Low' | 'Medium' | 'High';

export type TrackMode = 'None' | 'Surf' | 'Click';

export type Severity = 'Unknown' | 'Low' | 'Medium' | 'High' | 'Critical';

export type Priority = 'Low' | 'Medium' | 'High' | 'Critical';

export type AnalysisStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';

export type SimulationStatus = 'Draft' | 'Active' | 'Completed' | 'Archived';
