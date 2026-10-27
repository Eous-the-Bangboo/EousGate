from pathlib import Path
root=Path(__file__).parent
shell=(root/'shell.html').read_text(encoding='utf-8-sig')
parts=[]
for name in ['file','web','folder','program','general','about']:
    part=(root/'parts'/f'{name}.html').read_text(encoding='utf-8-sig')
    parts.append(f'<template x-if="panel===\'{name}\'"><div>{part}</div></template>')
shell=shell.replace('{{PANELS}}','\n'.join(parts)).replace('{{SURFACES}}',(root/'parts'/'surfaces.html').read_text(encoding='utf-8-sig'))
css=(root/'styles.css').read_text(encoding='utf-8-sig')+'\n'+(root/'parts'/'surfaces.css').read_text(encoding='utf-8-sig')
js=(root/'app.js').read_text(encoding='utf-8-sig')
alpine=(root/'vendor'/'alpine.min.js').read_text(encoding='utf-8-sig')
output='<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Eous 助手 · A 工具清单</title><link rel="icon" href="data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 32 32%22%3E%3Cpath fill=%22%23B54135%22 d=%22M5 6h22v4H5zm0 8h16v4H5zm0 8h22v4H5z%22/%3E%3C/svg%3E"><style>'+css+'</style></head><body>'+shell+'<script>'+js+'</script><script>'+alpine+'</script></body></html>'
(root/'prototype.html').write_text(output,encoding='utf-8')
print(f'prototype.html: {len(output.encode("utf-8"))} bytes')
