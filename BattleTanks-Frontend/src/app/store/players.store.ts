import { computed, inject } from '@angular/core';
import { signalStore, withState, withMethods, withComputed, patchState, withHooks } from '@ngrx/signals';
import { PlayerInfo, PlayerPosition } from '../models';
import { Game } from '../services/game';

export type PlayerStatus = 'Alive' | 'Dead';

export interface PlayerState extends PlayerInfo {
  position?: PlayerPosition;
  health: number;
  score: number;
}

export type PlayersState = {
  players: PlayerState[];
  localPlayerId: string | null;
  localUsername: string | null;
};

const initialState: PlayersState = {
  players: [],
  localPlayerId: null,
  localUsername: null,
};

const MAX_HEALTH = 3;

export const PlayerStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    alivePlayers: computed(() => store.players().filter((p) => p.health > 0)),
  })),
  withMethods((store) => ({
    addPlayer(player: PlayerInfo): void {
      patchState(store, (state) => {
        if (state.players.some((p) => p.id === player.id)) {
          return { players: state.players };
        }
        const newPlayer: PlayerState = {
          ...player,
          health: MAX_HEALTH,
          score: 0,
        };
        return { players: [...state.players, newPlayer] };
      });
    },
    removePlayer(id: string): void {
      patchState(store, (state) => ({
        players: state.players.filter((p) => p.id !== id),
      }));
    },
    updatePlayerPosition(id: string, position: PlayerPosition): void {
      patchState(store, (state) => ({
        players: state.players.map((p) =>
          p.id === id ? { ...p, position } : p
        ),
      }));
    },
    removePlayerById(id: string): void {
      patchState(store, (state) => ({
        players: state.players.filter((p) => p.id !== id),
      }));
    },
    damagePlayer(id: string): void {
      patchState(store, (state) => ({
        players: state.players.map((p) =>
          p.id === id ? { ...p, health: Math.max(0, p.health - 1) } : p
        ),
      }));
    },
    incrementScore(id: string, points: number): void {
      patchState(store, (state) => ({
        players: state.players.map((p) =>
          p.id === id ? { ...p, score: p.score + points } : p
        ),
      }));
    },
    setLocalPlayerId(id: string): void {
      patchState(store, (state) => ({
        localPlayerId: id,
        players: state.players.map((p) =>
          p.username === state.localUsername ? { ...p, id } : p
        ),
      }));
    },
    setLocalUsername(username: string): void {
      patchState(store, { localUsername: username });
    },
    getPlayerStatus(health: number): PlayerStatus {
      return health > 0 ? 'Alive' : 'Dead';
    },
  })),
  withHooks({
    onInit(store) {
      const gameService = inject(Game);

      gameService.connect();

      gameService.onWelcome?.((payload) => {
        store.setLocalPlayerId(payload.id);
      });

      gameService.onPlayerJoin((player) => {
        store.addPlayer(player);
      });

      gameService.onPlayerLeave((player) => {
        if (player.id) {
          store.removePlayerById(player.id);
        }
      });

      gameService.onPlayerMove((pos) => {
        if (pos.id) {
          store.updatePlayerPosition(pos.id, pos);
        }
      });
    },
  })
);
