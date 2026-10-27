# A 方向深化：制作约定

目标：完整可操作 HTML 设计原型，不改 WPF，不操作真实程序。保持白灰工具清单、朱红关键动作、平面分组、Microsoft YaHei + Segoe UI Variable、14px正文/13px辅助。
输出目录：D:/EousGate/docs/design-exploration/2026-10-20-reset/direction-a/parts
主Agent将片段组装为单页 prototype.html，Alpine.js已本地可用。各片段是抽屉内容，不含html/body/style/script，不重复抽屉header/footer。
抽屉内容宽约680px（padding左右28），高度约500px，可纵向滚动。不要缩字号塞内容。所有控件真实x-model，按钮不要有空动作。所有字段必须有label。
通用状态：panel ('file','web','folder','program','general','surfaces','about'或空)，draft 当前设置草稿对象，tab='trigger' (file用)，fieldError=''，saveError=''，saving=false，dirty getter比较draft与保存值。
通用行为：@click='...'，@change='...'，x-model='draft.xxx'，数字x-model.number；不写本地保存代码，根统一saveDraft/closePanel。表单内button必须type=button。
可用共用CSS：.section-title（16px/600，margin-bottom12），.section-help（14px/line1.65，次要灰），.field（margin-bottom20），.field-head（flex标题与操作），.field-label（14px/600），.help（13px/1.6次要色），.form-row（display:flex; align center;justify-between;gap20;padding16px 0;border-bottom细线），.stack（grid gap12），.inline（flex align center gap10），.form-grid（grid两等列 gap16），.input、.select、.textarea（1px灰边，6px圆角，14px），.btn、.btn.primary、.btn.danger、.btn.small、.text-btn（14px）、.check（flex gap10 align baseline）、.segment（inline-flex按钮组合）、.notice（浅灰底、13px、14pxpadding）、.error（红字13px）、.empty（padding32 textaligncenter）、.app-list（平面列表）、.app-row（flex align center justify-between;minheight56;border-bottom细线）、.tag（灰字13px）、.divider（margin24px上下1px线）。图标可用svg内联24x24、stroke1.6；不加emoji。
避免卡片套卡；结构文字不提“模拟/仅本机保存/原型”；制作边界放交付说明。

## draft 字段
file: {left:true,right:false,top:false,count:4,collapse:2,extension:'.pdf',category:'文档',search:'',showCategories:false,apps:[{name:'Microsoft Edge',detail:'系统默认',enabled:true},{name:'Adobe Acrobat',detail:'PDF 阅读与批注',enabled:true},{name:'WPS Office',detail:'文档与 PDF',enabled:true},{name:'Google Chrome',detail:'浏览器',enabled:false}]}
web: {enabled:true,side:'right',seconds:5,autoOpen:false,repeats:3}
folder: {record:true,button:true,search:'',tab:'recent'}
program: {name:'',path:'',args:'',force:false}；programId当前编辑工具id或空，editingProgramRunning getter，isCustomProgram getter
 general: {theme:'light',diagnostics:false}

## 根方法（可直接调用）
setFileTab('trigger'/'apps')，selectExtension(value)（保留各后缀草稿），moveApp(index,delta)，restoreRecommendations()（必须先展示范围确认），fileApps getter（当前后缀过滤后候选软件），filteredExtensions getter，openSurface('open'/'links'/'folders')，setFolderTab('recent'/'favorite'/'open')，filteredFolders getter，toggleFavorite(id)，removeFolder(id)，askClearHistory()，importHistory()（仅交互占位，不触碰本地文件，说明在交付文档），chooseExe()（原型提供路径输入），removeProgram()（先范围确认，不停止真实程序），showToast(text)；selectedTool getter；copy用navigator不需要。
folder数组项{id,name,path,time,favorite,open}；folder 行操作@dblclick="chooseFolder(folder)"，@click="toggleFavorite(folder.id)" / removeFolder(folder.id)；不能x-for变量叫window等。
openSurface打开一个可交互独立浮层展示，根实现。在工具设置中只需一个“查看打开面板/查看链接提示/打开目录选择”按钮，不做整套浮层。
文件类别允许源常见列表直接文字；搜索下拉按需展开。
