import { PlayerPosition } from './game-position.model';

export interface PlayerInfo {
  username: string;
  id?: string;
}

export interface ChatMessage {
  username: string;
  text: string;
  timestamp?: number;
}

export interface GameState {
  players: { id: string; username: string; position: PlayerPosition }[];
  status: 'waiting' | 'playing' | 'finished';
}

export type GameMessage =
  | { type: 'move'; payload: PlayerPosition }
  | { type: 'chat'; payload: ChatMessage }
  | { type: 'join'; payload: PlayerInfo }
  | { type: 'leave'; payload: PlayerInfo }
  | { type: 'state'; payload: GameState }
  | { type: 'welcome'; payload: { id: string } }
  | { type: 'destroy_block'; payload: { row: number; col: number; id?: string } }
  | { type: 'shoot'; payload: { id: string; x: number; y: number; direction: string } };
