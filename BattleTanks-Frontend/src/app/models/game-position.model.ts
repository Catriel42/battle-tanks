export type Direction = 'UP' | 'DOWN' | 'LEFT' | 'RIGHT';

export interface PlayerPosition {
  id?: string;
  x: number;
  y: number;
  direction?: Direction;
}

export interface Bullet {
  x: number;
  y: number;
  direction: Direction;
  ownerId: string;
}
