import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface Room {
  id: string;
  mapName: string;
  maxPlayers: number;
  currentPlayers: number;
  status: string;
}

@Injectable({
  providedIn: 'root'
})
export class RoomService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5000/api/room';

  constructor() {}

  getRooms(): Observable<Room[]> {
    const start = performance.now();
    return this.http.get<Room[]>(this.apiUrl).pipe(
      tap(() => {
        const end = performance.now();
        console.log(`[Benchmarking] GET /api/room tardó ${Math.round(end - start)} ms`);
      })
    );
  }

  createRoom(mapName: string, maxPlayers: number): Observable<Room> {
    const start = performance.now();
    return this.http.post<Room>(this.apiUrl, { mapName, maxPlayers }).pipe(
      tap(() => {
        const end = performance.now();
        console.log(`[Benchmarking] POST /api/room tardó ${Math.round(end - start)} ms`);
      })
    );
  }

  joinRoom(roomId: string): Observable<any> {
    const start = performance.now();
    return this.http.put(`${this.apiUrl}/${roomId}/join`, {}).pipe(
      tap(() => {
        const end = performance.now();
        console.log(`[Benchmarking] PUT /api/room/${roomId}/join tardó ${Math.round(end - start)} ms`);
      })
    );
  }
}
