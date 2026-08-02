/**
 * Represents a 2D position for a player on the game canvas.
 */
export type Direction = 'UP' | 'DOWN' | 'LEFT' | 'RIGHT';

export interface PlayerPosition {
  id?: string;
  x: number;
  y: number;
  direction?: Direction;
}
