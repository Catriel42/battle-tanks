import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { RoomResponse, CreateRoomRequest, MapResponse } from '../models';

@Injectable({
  providedIn: 'root'
})
export class RoomService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5000/api';
  
  // Available rooms cache
  private _rooms = signal<RoomResponse[]>([]);
  private _maps = signal<MapResponse[]>([]);
  
  readonly rooms = this._rooms.asReadonly();
  readonly maps = this._maps.asReadonly();

  /**
   * Get all available rooms (waiting status)
   */
  getRooms(): Observable<RoomResponse[]> {
    return this.http.get<RoomResponse[]>(`${this.apiUrl}/room`).pipe(
      tap(rooms => this._rooms.set(rooms))
    );
  }

  /**
   * Get a specific room by ID
   */
  getRoom(roomId: string): Observable<RoomResponse> {
    return this.http.get<RoomResponse>(`${this.apiUrl}/room/${roomId}`);
  }

  /**
   * Create a new room
   */
  createRoom(request: CreateRoomRequest): Observable<RoomResponse> {
    return this.http.post<RoomResponse>(`${this.apiUrl}/room`, request);
  }

  /**
   * Join a room (REST API - adds player to DB)
   * Note: After this, you need to call gameService.joinRoom() to join SignalR
   */
  joinRoom(roomId: string): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/room/${roomId}/join`, {});
  }

  /**
   * Leave a room (REST API)
   */
  leaveRoom(roomId: string): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/room/${roomId}/leave`, {});
  }

  /**
   * Start the game (REST API - host only)
   * Note: This just updates DB status; actual game start happens via SignalR
   */
  startRoom(roomId: string): Observable<{ message: string; roomId: string }> {
    return this.http.put<{ message: string; roomId: string }>(`${this.apiUrl}/room/${roomId}/start`, {});
  }

  /**
   * Get all available maps
   */
  getMaps(): Observable<MapResponse[]> {
    return this.http.get<MapResponse[]>(`${this.apiUrl}/maps`).pipe(
      tap(maps => this._maps.set(maps))
    );
  }

  /**
   * Get a specific map with tile data
   */
  getMap(mapId: string): Observable<MapResponse & { tileData: string }> {
    return this.http.get<MapResponse & { tileData: string }>(`${this.apiUrl}/maps/${mapId}`);
  }
}
