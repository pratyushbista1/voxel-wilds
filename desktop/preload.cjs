const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('desktop', {
  onCloseRequested: (callback) => {
    ipcRenderer.on('game:save-before-close', () => callback());
    ipcRenderer.send('game:ready');
  },
  finishClose: (saved) => ipcRenderer.send('game:close-result', saved === true),
  quit: () => ipcRenderer.send('game:quit'),
});
