export interface NetworkEvent {
  id: string;
  source: string;
  action: string;
  timestamp: string;
  origH: string;
  respH: string;
  proto: string;
  service: string;
  mlScore: string;
}
