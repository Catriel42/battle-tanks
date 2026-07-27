import { inject } from '@angular/core';
import { signalStore, withState, withMethods, patchState, withHooks } from '@ngrx/signals';
import { PlayerInfo, PlayerPosition } from '../models';
import { Game } from '../services/game';

export interface PlayerState extends PlayerInfo {
  position?: PlayerPosition;
  health?: number;
}

export type PlayersState = {
  players: PlayerState[];
};

const initialState: PlayersState = {
  players: [],
};

export const PlayerStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => ({
    addPlayer(player: PlayerState): void {
      patchState(store, (state) => {
        if (state.players.some((p) => p.username === player.username)) {
          return { players: state.players };
        }
        console.log('[PlayerStore] Adding player:', player);
        const newPlayer = { ...player, health: player.health ?? 100 };
        return { players: [...state.players, newPlayer] };
      });
    },
    removePlayer(username: string): void {
      patchState(store, (state) => ({
        players: state.players.filter((p) => p.username !== username),
      }));
    },
    updatePlayerPosition(id: string, position: PlayerPosition): void {
      console.log('[PlayerStore] Updating position for ID:', id, position);
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
  })),
  withHooks({
    onInit(store) {
      const gameService = inject(Game);

      gameService.connect();

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
