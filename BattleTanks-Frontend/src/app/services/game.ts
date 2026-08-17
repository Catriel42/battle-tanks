import { Service } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Subscription } from 'rxjs';

import { PlayerPosition, PlayerInfo, ChatMessage, GameState } from '../models';

@Service()
export class Game {
  private hubConnection: signalR.HubConnection | null = null;
  private readonly HUB_URL = 'http://localhost:5000/gamehub';

  private connectionStatus$ = new Subject<boolean>();
  private welcome$ = new Subject<{ id: string }>();
  private playerJoin$ = new Subject<PlayerInfo>();
  private playerLeave$ = new Subject<PlayerInfo>();
  private playerMove$ = new Subject<PlayerPosition>();
  private chatMessage$ = new Subject<ChatMessage>();
  private gameState$ = new Subject<GameState>();
  private shoot$ = new Subject<{ id: string; x: number; y: number; direction: string }>();
  private destroyBlock$ = new Subject<{ row: number; col: number; id?: string }>();

  connect(url?: string): void {
    if (this.hubConnection) {
      this.disconnect();
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(url ?? this.HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('ReceiveWelcome', (payload: { id: string }) => this.welcome$.next(payload));
    this.hubConnection.on('ReceivePlayerJoin', (payload: PlayerInfo) => this.playerJoin$.next(payload));
    this.hubConnection.on('ReceivePlayerLeave', (payload: PlayerInfo) => this.playerLeave$.next(payload));
    this.hubConnection.on('ReceivePlayerMove', (payload: PlayerPosition) => this.playerMove$.next(payload));
    this.hubConnection.on('ReceiveChatMessage', (payload: ChatMessage) => this.chatMessage$.next(payload));
    this.hubConnection.on('ReceiveShoot', (payload: { id: string; x: number; y: number; direction: string }) => this.shoot$.next(payload));
    this.hubConnection.on('ReceiveDestroyBlock', (payload: { row: number; col: number; id?: string }) => this.destroyBlock$.next(payload));

    this.hubConnection.onreconnecting(() => this.connectionStatus$.next(false));
    this.hubConnection.onreconnected(() => this.connectionStatus$.next(true));
    this.hubConnection.onclose(() => this.connectionStatus$.next(false));

    this.hubConnection
      .start()
      .then(() => {
        console.log('[GameService] SignalR connected');
        this.connectionStatus$.next(true);
      })
      .catch((err) => console.error('[GameService] SignalR connection error:', err));
  }

  disconnect(): void {
    this.hubConnection?.stop();
    this.hubConnection = null;
  }

  sendPlayerJoin(username: string): void {
    if (!this.hubConnection) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.hubConnection.invoke('SendPlayerJoin', username);
  }

  onPlayerJoin(callback: (info: PlayerInfo) => void): Subscription {
    return this.playerJoin$.subscribe(callback);
  }

  sendPlayerMove(position: PlayerPosition): void {
    if (!this.hubConnection) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.hubConnection.invoke('SendPlayerMove', position.x, position.y, position.direction ?? 'UP');
  }

  onPlayerMove(callback: (position: PlayerPosition) => void): Subscription {
    return this.playerMove$.subscribe(callback);
  }

  sendChatMessage(message: ChatMessage): void {
    if (!this.hubConnection) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.hubConnection.invoke('SendChatMessage', message.username, message.text);
  }

  onChatMessage(callback: (message: ChatMessage) => void): Subscription {
    return this.chatMessage$.subscribe(callback);
  }

  onPlayerLeave(callback: (info: PlayerInfo) => void): Subscription {
    return this.playerLeave$.subscribe(callback);
  }

  onWelcome(callback: (payload: { id: string }) => void): Subscription {
    return this.welcome$.subscribe(callback);
  }

  onGameState(callback: (state: GameState) => void): Subscription {
    return this.gameState$.subscribe(callback);
  }

  sendShoot(id: string, x: number, y: number, direction: string): void {
    if (!this.hubConnection) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.hubConnection.invoke('SendShoot', x, y, direction);
  }

  onShoot(callback: (payload: { id: string; x: number; y: number; direction: string }) => void): Subscription {
    return this.shoot$.subscribe(callback);
  }

  sendDestroyBlock(row: number, col: number, id?: string): void {
    if (!this.hubConnection) {
      console.warn('[GameService] Cannot send message — not connected.');
      return;
    }
    this.hubConnection.invoke('SendDestroyBlock', row, col);
  }

  onDestroyBlock(callback: (payload: { row: number; col: number; id?: string }) => void): Subscription {
    return this.destroyBlock$.subscribe(callback);
  }

  getConnectionStatus$() {
    return this.connectionStatus$.asObservable();
  }
}
