const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

app.setAppUserModelId('io.voxelwilds.game');
app.commandLine.appendSwitch('disable-frame-rate-limit');
const root = app.getAppPath();
const portableDir = process.env.PORTABLE_EXECUTABLE_DIR;
const customData = process.env.VOXEL_DATA_DIR;
const testing = process.env.VOXEL_TEST === '1';
if (customData || portableDir || !app.isPackaged) {
  app.setPath('userData', customData || path.join(portableDir || root, 'userdata'));
}
if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  let window,
    server,
    allowClose = false,
    address = '',
    rendererReady = false,
    closeTimer;
  app.on('second-instance', () => {
    if (window) {
      if (window.isMinimized()) window.restore();
      window.focus();
    }
  });
  app
    .whenReady()
    .then(async () => {
      const { createGameServer } = await import(pathToFileURL(path.join(root, 'server.mjs')).href);
      const saveDir =
        process.env.VOXEL_SAVE_DIR ||
        (!app.isPackaged && !customData
          ? path.join(root, 'saves')
          : path.join(app.getPath('userData'), 'saves'));
      server = createGameServer({
        saveDir,
        addonDir: app.isPackaged ? path.join(process.resourcesPath, 'three-addons') : undefined,
      });
      await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
      });
      address = `http://127.0.0.1:${server.address().port}`;
      Menu.setApplicationMenu(null);
      window = new BrowserWindow({
        width: 1366,
        height: 820,
        minWidth: 900,
        minHeight: 620,
        title: 'Voxel Wilds',
        backgroundColor: '#17241e',
        show: false,
        icon: path.join(root, 'build', 'icon.ico'),
        webPreferences: {
          preload: path.join(__dirname, 'preload.cjs'),
          contextIsolation: true,
          nodeIntegration: false,
          sandbox: true,
          backgroundThrottling: !testing,
        },
      });
      window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
      window.webContents.on('will-navigate', (event, url) => {
        if (!url.startsWith(address + '/')) event.preventDefault();
      });
      window.webContents.session.setPermissionRequestHandler((contents, permission, callback) =>
        callback(
          contents === window.webContents && ['pointerLock', 'fullscreen'].includes(permission)
        )
      );
      window.webContents.on('before-input-event', (event, input) => {
        if (input.key === 'F11' && input.type === 'keyDown') {
          event.preventDefault();
          window.setFullScreen(!window.isFullScreen());
        }
      });
      window.on('close', (event) => {
        if (allowClose || !rendererReady || window.webContents.isCrashed()) return;
        event.preventDefault();
        if (closeTimer) return;
        window.webContents.send('game:save-before-close');
        closeTimer = setTimeout(async () => {
          closeTimer = null;
          const { response } = await dialog.showMessageBox(window, {
            type: 'warning',
            title: 'Game not responding',
            message:
              'The game has not finished saving. Closing now may lose changes since the last autosave.',
            buttons: ['Keep waiting', 'Close anyway'],
            defaultId: 0,
            cancelId: 0,
          });
          if (response === 1 && !window.isDestroyed()) {
            allowClose = true;
            window.close();
          }
        }, 10000);
      });
      ipcMain.on('game:ready', (event) => {
        if (event.sender === window.webContents) rendererReady = true;
      });
      ipcMain.on('game:close-result', async (event, saved) => {
        if (event.sender !== window.webContents) return;
        clearTimeout(closeTimer);
        closeTimer = null;
        if (!saved) {
          const { response } = await dialog.showMessageBox(window, {
            type: 'warning',
            title: 'Save failed',
            message: 'The world could not be saved.',
            buttons: ['Keep playing', 'Exit without saving'],
            defaultId: 0,
            cancelId: 0,
          });
          if (response !== 1) return;
        }
        allowClose = true;
        window.close();
      });
      ipcMain.on('game:quit', (event) => {
        if (event.sender === window.webContents) window.close();
      });
      await window.loadURL(address + (testing ? '/?test=1' : '/'));
      if (!testing) window.show();
    })
    .catch((error) => {
      dialog.showErrorBox('Voxel Wilds', error.message);
      app.quit();
    });
  app.on('window-all-closed', () => {
    server?.close();
    app.quit();
  });
}
