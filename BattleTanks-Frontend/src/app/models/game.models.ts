// ==================== Enums ====================

export type Direction = 'up' | 'down' | 'left' | 'right';
export type GameStatus = 'waiting' | 'starting' | 'playing' | 'finished';
export type InputType = 'move_start' | 'move_stop' | 'shoot';

// ==================== Input DTOs (Client → Server) ====================

export interface PlayerInputDto {
  type: InputType;
  direction?: Direction;
  sequenceNumber: number;
}

// ==================== State Snapshot DTOs (Server → Client) ====================

export interface TankDto {
  id: string;           // ConnectionId
  playerId: string;     // Guid
  username: string;
  x: number;
  y: number;
  direction: Direction;
  isMoving: boolean;
  health: number;
  lives: number;
  isAlive: boolean;
  isEliminated: boolean;
}

export interface BulletDto {
  id: string;
  ownerId: string;      // ConnectionId of shooter
  x: number;
  y: number;
  direction: Direction;
}

export interface GameStateSnapshot {
  tick: number;
  status: GameStatus;
  serverTime: number;
  tanks: TankDto[];
  bullets: BulletDto[];
  destroyedBlocks: number[];  // Flattened: [row1, col1, row2, col2, ...]
}

// ==================== Event DTOs (Server → Client) ====================

export interface ConnectedEvent {
  connectionId: string;
  playerId: string;
  username: string;
  serverTime: number;
}

export interface JoinedRoomEvent {
  roomId: string;
  tank: TankDto;
  mapWidth: number;
  mapHeight: number;
  mapGrid: number[][];
  lives: number;
  maxPlayers: number;
  minPlayers: number;
  status: GameStatus;
}

export interface GameStartingEvent {
  countdownSeconds: number;
  tanks: TankDto[];
  mapWidth: number;
  mapHeight: number;
  mapGrid: number[][];
}

export interface GameStartedEvent {
  startTime: number;
  tanks: TankDto[];
}

export interface PlayerJoinedEvent {
  tank: TankDto;
  totalPlayers: number;
}

export interface PlayerLeftEvent {
  playerId: string;
  username: string;
  totalPlayers: number;
}

export interface PlayerHitEvent {
  victimId: string;
  attackerId: string;
  damage: number;
  remainingHealth: number;
  remainingLives: number;
}

export interface PlayerKilledEvent {
  victimId: string;
  victimUsername: string;
  killerId: string;
  killerUsername: string;
  victimLivesRemaining: number;
}

export interface PlayerEliminatedEvent {
  playerId: string;
  username: string;
  position: number;       // Final position (4th, 3rd, 2nd)
  playersRemaining: number;
}

export interface PlayerRespawnedEvent {
  playerId: string;
  x: number;
  y: number;
}

export interface BlockDestroyedEvent {
  row: number;
  col: number;
  destroyerId?: string;
}

export interface BulletFiredEvent {
  bullet: BulletDto;
}

export interface BulletHitEvent {
  bulletId: string;
  hitPlayerId?: string;
  hitRow?: number;
  hitCol?: number;
}

export interface PlayerStatsDto {
  playerId: string;
  username: string;
  position: number;
  kills: number;
  deaths: number;
  shotsFired: number;
  shotsHit: number;
  accuracy: number;
  blocksDestroyed: number;
  damageDealt: number;
  damageTaken: number;
  survivalTimeMs: number;
}

export interface GameOverEvent {
  winnerId?: string;
  winnerUsername?: string;
  finalStats: PlayerStatsDto[];
  durationMs: number;
}

// ==================== Lobby/Room Events ====================

export interface PlayerInfoDto {
  playerId: string;
  username: string;
  isHost: boolean;
  isReady: boolean;
}

export interface RoomStateEvent {
  roomId: string;
  status: GameStatus;
  players: PlayerInfoDto[];
  maxPlayers: number;
  minPlayers: number;
  canStart: boolean;
  hostId?: string;
}

// ==================== Error Events ====================

export interface ErrorEvent {
  code: string;
  message: string;
}

// ==================== REST API DTOs ====================

export interface MapResponse {
  id: string;
  name: string;
  width: number;
  height: number;
}

export interface MapDetailResponse extends MapResponse {
  tileData: string;  // JSON string of int[][]
}

export interface RoomResponse {
  id: string;
  status: GameStatus;
  mapId: string;
  mapName: string;
  maxPlayers: number;
  minPlayers: number;
  lives: number;
  currentPlayers: number;
  canStart: boolean;
}

export interface CreateRoomRequest {
  mapId: string;
  maxPlayers?: number;
  lives?: number;
}

// ==================== Chat ====================

export interface ChatMessage {
  username: string;
  text: string;
  timestamp?: number;
}
