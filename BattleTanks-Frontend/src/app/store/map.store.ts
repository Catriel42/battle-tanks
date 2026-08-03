import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';

export type MapState = {
  grid: number[][];
};

const initialState: MapState = {
  grid: [],
};

export const MapStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => ({
    loadMap(grid: number[][]): void {
      patchState(store, { grid });
    },
    destroyBlock(row: number, col: number): void {
      patchState(store, (state) => {
        const newGrid = [...state.grid];
        if (newGrid[row] && newGrid[row][col] === 2) {
          const newRow = [...newGrid[row]];
          newRow[col] = 0;
          newGrid[row] = newRow;
        }
        return { grid: newGrid };
      });
    },
  }))
);
