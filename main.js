const {app,BrowserWindow,WebContentsView,ipcMain,shell,dialog}=require('electron');
const fs=require('fs');
const path=require('path');
const EA_URL='https://www.ea.com/games/ea-sports-college-football/team-builder';
let window,portal;
function create(){
 window=new BrowserWindow({width:1400,height:950,minWidth:950,minHeight:650,webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
 window.loadFile('index.html');
 portal=new WebContentsView({webPreferences:{nodeIntegration:false,contextIsolation:true,sandbox:true,partition:'persist:ea-teamcraft'}});
 window.contentView.addChildView(portal);
 portal.setBounds({x:0,y:0,width:0,height:0});
 portal.webContents.setWindowOpenHandler(({url})=>{if(url.startsWith('https://'))shell.openExternal(url);return {action:'deny'}});
 portal.webContents.on('will-navigate',(e,url)=>{if(!url.startsWith('https://'))e.preventDefault()});
 portal.webContents.loadURL(EA_URL).catch(()=>{});
 window.on('closed',()=>{window=null;portal=null});
}
app.whenReady().then(create);
app.on('window-all-closed',()=>{if(process.platform!=='darwin')app.quit()});
app.on('activate',()=>{if(!BrowserWindow.getAllWindows().length)create()});
ipcMain.handle('save',async(_,data)=>{const {filePath}=await dialog.showSaveDialog(window,{defaultPath:'team.teamcraft',filters:[{name:'TeamCraft project',extensions:['teamcraft']}]});if(!filePath)return false;fs.writeFileSync(filePath,JSON.stringify(data,null,2));return true});
ipcMain.handle('load',async()=>{const {filePaths}=await dialog.showOpenDialog(window,{properties:['openFile'],filters:[{name:'TeamCraft project',extensions:['teamcraft']}]});return filePaths?.length?JSON.parse(fs.readFileSync(filePaths[0],'utf8')):null});
ipcMain.handle('portal-bounds',(_,bounds)=>{if(!portal||!window)return;const {x,y,width,height}=bounds||{};if(![x,y,width,height].every(Number.isFinite))return;const [w,h]=window.getContentSize();portal.setBounds({x:Math.max(0,Math.min(w,Math.round(x))),y:Math.max(0,Math.min(h,Math.round(y))),width:Math.max(0,Math.min(w-Math.max(0,Math.round(x)),Math.round(width))),height:Math.max(0,Math.min(h-Math.max(0,Math.round(y)),Math.round(height)))});});
ipcMain.handle('portal-reload',()=>portal?.webContents.reload());
ipcMain.handle('portal-external',()=>shell.openExternal(portal?.webContents.getURL()||EA_URL));
