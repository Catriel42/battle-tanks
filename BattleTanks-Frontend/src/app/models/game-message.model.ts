import { PlayerPosition } from './player-position.model';

/**
 * Information about a player joining the game.
 */
export interface PlayerInfo {
  username: string;
  id?: string;
}

/**
 * A chat message sent between players.
 */
export interface ChatMessage {
  username: string;
  text: string;
  timestamp?: number;
}

/**
 * The current state of the game, broadcast by the server.
 */
export interface GameState {
  players: { id: string; username: string; position: PlayerPosition }[];
  status: 'waiting' | 'playing' | 'finished';
}

/**
 * Discriminated union for all WebSocket messages.
 *
 * TypeScript uses the `type` field to narrow the payload automatically.
 * For example:
 *
 *   if (msg.type === 'move') {
 *     msg.payload.x;  // ✅ TypeScript knows payload is PlayerPosition
 *   }
 */
export type GameMessage =
  | { type: 'move'; payload: PlayerPosition }
  | { type: 'chat'; payload: ChatMessage }
  | { type: 'join'; payload: PlayerInfo }
  | { type: 'leave'; payload: PlayerInfo }
  | { type: 'state'; payload: GameState };
