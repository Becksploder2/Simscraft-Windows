const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('teamcraft',{
 save:x=>ipcRenderer.invoke('save',x),load:()=>ipcRenderer.invoke('load'),
 portalBounds:bounds=>ipcRenderer.invoke('portal-bounds',bounds),
 portalReload:()=>ipcRenderer.invoke('portal-reload'),
 portalExternal:()=>ipcRenderer.invoke('portal-external')
});
