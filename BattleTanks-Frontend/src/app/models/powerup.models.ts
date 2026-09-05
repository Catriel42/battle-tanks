export interface PowerUpSpawnedEvent {
  id: string;
  type: 'ExtraLife';
  x: number;
  y: number;
  timestamp: number;
  sent_at: number;
}

export interface PowerUpCollectedEvent {
  id: string;
  player_id: string;
  username: string;
  new_lives: number;
  timestamp: number;
  sent_at: number;
}

export interface PowerUpState {
  id: string;
  type: 'ExtraLife';
  x: number;
  y: number;
  isCollected: boolean;
}

export interface CollisionEvent {
  attacker_id: string;
  victim_id: string;
  damage: number;
  timestamp: number;
  sent_at: number;
}

export interface GameOverMqttEvent {
  winner_id: string;
  winner_username: string;
  timestamp: number;
  sent_at: number;
}
