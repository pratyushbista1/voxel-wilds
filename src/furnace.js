import { FUEL, SMELTING } from './recipes.js';
import { makeStack } from './inventory.js';

export const makeFurnace = () => ({
  slots: [null, null, null],
  burn: 0,
  burnTotal: 0,
  progress: 0,
  inputId: 0,
});

export function tickFurnace(furnace, dt) {
  const [input, fuel, output] = furnace.slots;
  const result = SMELTING[input?.id];
  const ready = result && (!output || (output.id === result && output.count < 64));
  if (furnace.inputId !== (input?.id || 0)) {
    furnace.inputId = input?.id || 0;
    furnace.progress = 0;
  }
  furnace.burn = Math.max(0, furnace.burn - dt);
  if (!furnace.burn && ready && FUEL[fuel?.id]) {
    furnace.burn = furnace.burnTotal = FUEL[fuel.id];
    fuel.count--;
    if (!fuel.count) furnace.slots[1] = null;
  }
  if (ready && furnace.burn > 0) {
    furnace.progress += dt;
    if (furnace.progress >= 10) {
      furnace.progress -= 10;
      if (output) output.count++;
      else furnace.slots[2] = makeStack(result);
      input.count--;
      if (!input.count) furnace.slots[0] = null;
      return true;
    }
  } else furnace.progress = Math.max(0, furnace.progress - dt * 2);
  return false;
}
