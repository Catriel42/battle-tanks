import { Service } from '@angular/core';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { Subject, Subscription } from 'rxjs';
import { filter, map } from 'rxjs/operators';

import {
  GameMessage,
  PlayerPosition,
  PlayerInfo,
  ChatMessage,
  GameState,
} from '../models';

@Service()
export class Game {
  private socket$: WebSocketSubject<GameMessage> | null = null;
  private connectionStatus$ = new Subject<boolean>();
  private readonly DEFAULT_URL = 'ws://localhost:5000/ws';

  connect(url?: string): void {
    if (this.socket$) {
      this.disconnect();
    }

    this.socket$ = webSocket<GameMessage>({
      url: url ?? this.DEFAULT_URL,
      openObserver: {
        next: () => {
          console.log('[GameService] WebSocket connection opened');
          this.connectionStatus$.next(true);
        },
      },
      closeObserver: {
        next: () => {
          console.log('[GameService] WebSocket connection closed');
          this.connectionStatus$.next(false);
        },
      },
    });

    this.socket$.subscribe({
      error: (err) => console.error('[GameService] WebSocket error:', err),
    });
  }

  disconnect(): void {
    if (this.socket$) {
      this.socket$.complete();
      this.socket$ = null;
    }
  }

  sendPlayerMove(position: PlayerPosition): void {
    this.send({ type: 'move', payload: position });
  }

  onPlayerMove(callback: (position: PlayerPosition) => void): Subscription | null {
    return this.onMessage('move', callback);
  }

  sendChatMessage(message: ChatMessage): void {
    this.send({ type: 'chat', payload: message });
  }

  onChatMessage(callback: (message: ChatMessage) => void): Subscription | null {
    return this.onMessage('chat', callback);
  }

  sendPlayerJoin(playerInfo: PlayerInfo): void {
    this.send({ type: 'join', payload: playerInfo });
  }

  onPlayerJoin(callback: (playerInfo: PlayerInfo) => void): Subscription | null {
    return this.onMessage('join', callback);
  }

  onPlayerLeave(callback: (playerInfo: PlayerInfo) => void): Subscription | null {
    return this.onMessage('leave', callback);
  }

  onGameState(callback: (state: GameState) => void): Subscription | null {
    return this.onMessage('state', callback);
  }

  onWelcome(callback: (payload: { id: string }) => void): Subscription | null {
    return this.onMessage('welcome', callback);
  }

  sendDestroyBlock(row: number, col: number, id?: string): void {
    this.send({ type: 'destroy_block', payload: { row, col, id } });
  }

  onDestroyBlock(callback: (payload: { row: number; col: number; id?: string }) => void): Subscription | null {
    return this.onMessage('destroy_block', callback);
  }

  sendShoot(id: string, x: number, y: number, direction: string): void {
    this.send({ type: 'shoot', payload: { id, x, y, direction } });
  }

  onShoot(callback: (payload: { id: string; x: number; y: number; direction: string }) => void): Subscription | null {
    return this.onMessage('shoot', callback);
  }

  getConnectionStatus$() {
    return this.connectionStatus$.asObservable();
  }

  private send(message: GameMessage): void {
    if (!this.socket$) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.socket$.next(message);
  }

  private onMessage<P>(
    type: GameMessage['type'],
    callback: (payload: P) => void
  ): Subscription | null {
    if (!this.socket$) {
      console.warn(`[GameService] Cannot listen for "${type}" — not connected.`);
      return null;
    }

    return this.socket$
      .pipe(
        filter((msg) => msg.type === type),
        map((msg) => {
          console.log(`[GameService] Received ${type}:`, msg.payload);
          return msg.payload as P;
        })
      )
      .subscribe({
        next: callback,
        error: (err) => console.error(`[GameService] Error on "${type}" listener:`, err),
      });
  }
}
