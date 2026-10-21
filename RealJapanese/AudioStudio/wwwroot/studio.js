(() => {
  'use strict';
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];
  const state = { catalog: [], references: [], runs: [], exports: [], view: 'generate', filter: '', selected: new Set(), groups: new Set(), activeRun: null, activeRunId: null, generatingRunId: null, cancelRequestedRunId: null, runActionBusyId: null, runTimer: null, pollingRunId: null, exportingRun: null, pollBusy: false, pendingChoiceSaves: new Map(), referenceFilter: '', exportFilter: '', exportEngine: 'all', runFilter: '', activeAudio: null, previewFile: null, cropStartEdited: false, cropDurationEdited: false };
  const groups = ['Nouns', 'Verbs', 'Adjectives', 'Words & expressions'];
  const engines = [{ id: 'bert', name: 'BERT', detail: 'Fixed voice · steady baseline' }, { id: 'fish', name: 'Fish', detail: 'Reference conditioned · natural variation' }, { id: 'irodori', name: 'Irodori', detail: 'Reference conditioned · expressive delivery' }];
  const quality = [{ id: 'OriginalWav', name: 'Original WAV', detail: 'Native sample rate, uncompressed WAV' }, { id: 'Wav24000Hz16BitMono', name: 'WAV · 24 kHz', detail: '16-bit mono, ready for editing' }, { id: 'Mp3Mono64Kbps', name: 'MP3 · 64 kbps', detail: 'Compact mono audio' }, { id: 'OpusMono32Kbps', name: 'Opus · 32 kbps', detail: 'Small files for web and apps' }];
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const safeUrl = raw => { if (!raw) return ''; try { const u = new URL(raw, location.href); return ['http:', 'https:'].includes(u.protocol) ? u.href : ''; } catch { return ''; } };
  const wordKey = w => w.key ?? `${w.dataset}:${w.id}`;
  const notice = (message, ok = false) => { const n = $('#notice'); n.textContent = message; n.classList.toggle('ok', ok); n.hidden = !message; if (message) { clearTimeout(notice.timer); notice.timer = setTimeout(() => { n.hidden = true; }, 7000); } };
  async function api(path, options = {}) {
    const response = await fetch(path, options);
    const type = response.headers.get('content-type') || '';
    const body = type.includes('json') ? await response.json() : await response.text();
    if (!response.ok) { const e = new Error(typeof body === 'object' ? body.error || `Request failed (${response.status})` : body || `Request failed (${response.status})`); e.status = response.status; e.data = body; throw e; }
    return body;
  }
  const studioHeaders = { 'X-Audio-Studio': '1' };
  const json = data => ({ method: 'POST', headers: { ...studioHeaders, 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  function nav(view) {
    state.view = view;
    $$('.view').forEach(el => el.hidden = el.id !== `view-${view}`);
    $$('.nav-item').forEach(el => el.classList.toggle('active', el.dataset.view === view));
    history.replaceState(null, '', `#${view}`);
    $('.sidebar').classList.remove('open'); $('#mobile-menu').setAttribute('aria-expanded', 'false');
    if (view === 'generate') renderGenerate();
    if (view === 'runs') loadRuns();
    if (view === 'exported') loadExports();
    if (view === 'references') loadReferences();
  }
  function pageHead(eyebrow, title, description, trailing = '') { return `<div class="page-head"><div><p class="eyebrow">${esc(eyebrow)}</p><h1>${esc(title)}</h1><p>${esc(description)}</p></div>${trailing}</div>`; }
  function panelHead(number, title, description) { return `<div class="panel-head"><div class="step-label"><span class="step-badge">${number}</span><div><h2>${title}</h2><p>${description}</p></div></div></div>`; }
  function catalogFiltered() { const q = state.filter.trim().toLocaleLowerCase(); return state.catalog.filter(w => (!state.missingOnly || !w.hasAudio) && (!q || [w.japanese, w.kana, w.english, w.group].some(x => String(x ?? '').toLocaleLowerCase().includes(q)))); }
  function visibleGroupCounts(list) { const o = Object.fromEntries(groups.map(g => [g, 0])); list.forEach(w => { if (o[w.group] !== undefined) o[w.group]++; }); return o; }
  function renderGenerate() {
    const root = $('#view-generate'), listScroll = $('.word-list', root)?.scrollTop || 0; if (!state.catalog.length) { root.innerHTML = `${pageHead('Audio workspace', 'Generate audio', 'Prepare model recordings for your vocabulary.') }<div class="panel"><div class="loading">${state.catalogLoaded ? 'No vocabulary was returned by the catalog.' : 'Loading vocabulary…'}</div></div>`; return; }
    const words = catalogFiltered(), counts = visibleGroupCounts(words), allCount = state.catalog.length, selectedCount = state.selected.size, hidden = [...state.selected].filter(k => !words.some(w => wordKey(w) === k)).length;
    const groupsMarkup = groups.map(g => { const keys=words.filter(w=>w.group===g).map(wordKey), picked=keys.filter(k=>state.selected.has(k)).length; return `<label class="group-toggle"><input type="checkbox" data-group="${esc(g)}" ${picked===keys.length&&keys.length?'checked':''} ${picked&&picked<keys.length?'data-mixed="true"':''} ${keys.length?'':'disabled'}><span class="group-name">${esc(g)}</span><span class="group-count">${picked}/${keys.length}</span></label>`; }).join('');
    const row = w => `<label class="word-row"><input type="checkbox" data-word="${esc(wordKey(w))}" ${state.selected.has(wordKey(w)) ? 'checked' : ''} aria-label="Select ${esc(w.japanese)}"><span class="word-jp">${esc(w.japanese)}</span><span class="word-reading">${esc(w.kana)}${w.spokenKana && w.spokenKana !== w.kana ? `<small style="display:block">Spoken as ${esc(w.spokenKana)}</small>` : ''}</span><span class="word-meaning">${esc(w.english)}${w.speechNote ? `<br><span class="word-source">${esc(w.speechNote)}</span>` : ''}</span><span class="word-source">${esc(w.group)} · ${w.hasAudio ? '<span class="pill success">Audio exported</span>' : 'No export yet'}</span></label>`;
    root.innerHTML = `${pageHead('Audio workspace', 'Generate audio', 'Choose vocabulary, models and a voice reference for your next recording run.', `<span class="step-count">${allCount} words in catalog</span>`)}
      <div class="panel"><div class="panel-inner">${panelHead('1', 'Choose words', 'Select by category or word. Search matches Japanese, reading, meaning and category.')}
      <div class="toolbar"><label class="search-wrap"><span class="search-icon" aria-hidden="true">⌕</span><input class="input" id="word-search" type="search" placeholder="Search all vocabulary…" value="${esc(state.filter)}" aria-label="Search vocabulary"></label><label class="inline-check"><input id="missing-only" type="checkbox" ${state.missingOnly ? 'checked' : ''}> Only words without exported audio</label></div>
      <div class="group-grid">${groupsMarkup}</div><div class="toolbar"><button class="button text small" id="select-visible">Select visible</button><button class="button text small" id="clear-visible">Clear visible</button><span class="word-count">Showing ${words.length} of ${allCount}</span></div>
      <div class="word-list" role="group" aria-label="Vocabulary selection"><div class="word-row list-header"><span></span><span>Japanese</span><span>Reading</span><span>Meaning</span><span>Catalog</span></div>${words.length ? words.map(row).join('') : '<div class="empty-state">No words match this search.</div>'}</div>
      <div class="selection-bar"><span><strong>${selectedCount}</strong> selected${hidden ? ` · ${hidden} selected word${hidden === 1 ? '' : 's'} hidden by current filters` : ''}</span><span class="selection-note">Selections stay selected when you search or change categories.</span></div></div></div>
      <div class="panel"><div class="panel-inner">${panelHead('2', 'Choose models', 'Choose one or more voices. Model generation can take a little while.')}
      <div class="model-grid">${engines.map(e => `<label class="check-card"><input type="checkbox" data-engine="${e.id}" ${state.engines?.includes(e.id) ? 'checked' : ''}><span><strong>${e.name}</strong><small>${e.detail}</small></span></label>`).join('')}</div></div></div>
      <div class="panel"><div class="panel-inner">${panelHead('3', 'Voice reference', 'Use one of your prepared references, or generate without a reference.')}
      ${referenceRadios()}</div></div>
      <div class="panel"><div class="panel-inner">${panelHead('4', 'Audio format', 'Choose the format saved with this run and its exports.')}
      <div class="quality-row">${quality.map((q,i) => `<label class="radio-tile"><input type="radio" name="quality" value="${q.id}" ${(state.quality || 'OriginalWav') === q.id ? 'checked' : ''}><span><strong>${q.name}${i===0?' · Default':''}</strong><small>${q.detail}</small></span></label>`).join('')}</div>
      <div class="form-actions"><span class="muted" id="submit-hint">${state.generatingRunId ? 'A generation run is already active.' : 'Select at least one word and model.'}</span><button id="start-run" class="button primary" ${selectedCount && state.engines?.length && !state.generatingRunId ? '' : 'disabled'}>Generate ${selectedCount ? `${selectedCount} word${selectedCount===1?'':'s'}` : 'audio'} <span aria-hidden="true">→</span></button></div></div></div>`;
    $('#word-search').addEventListener('input', e => { state.filter = e.target.value; const pos = e.target.selectionStart; renderGenerate(); $('#word-search').focus(); $('#word-search').setSelectionRange(pos,pos); });
    $('.word-list', root).scrollTop = listScroll;
    $('#missing-only').addEventListener('change', e => { state.missingOnly = e.target.checked; if (state.missingOnly) state.catalogVisible = state.catalog.filter(w => !w.hasAudio); renderGenerate(); });
    $$('.group-toggle input').forEach(el => { if(el.dataset.mixed)el.indeterminate=true; el.addEventListener('change', () => { const group=el.dataset.group, keys=words.filter(w=>w.group===group).map(wordKey); if(el.checked) keys.forEach(k=>state.selected.add(k)); else keys.forEach(k=>state.selected.delete(k)); renderGenerate(); $$('[data-group]').find(x=>x.dataset.group===group)?.focus(); }); });
    $$('.word-row input[data-word]').forEach(el => el.addEventListener('change', () => { const key=el.dataset.word; if (el.checked) state.selected.add(key); else state.selected.delete(key); renderGenerate(); $$('[data-word]').find(x=>x.dataset.word===key)?.focus(); }));
    $('#select-visible').addEventListener('click', () => { words.forEach(w => state.selected.add(wordKey(w))); renderGenerate(); });
    $('#clear-visible').addEventListener('click', () => { const visible = new Set(words.map(wordKey)); state.selected = new Set([...state.selected].filter(k => !visible.has(k))); renderGenerate(); });
    $$('[data-engine]').forEach(el => el.addEventListener('change', () => { const chosen = new Set(state.engines || []); el.checked ? chosen.add(el.dataset.engine) : chosen.delete(el.dataset.engine); state.engines = [...chosen]; renderGenerate(); }));
    $$('[name="quality"]').forEach(el => el.addEventListener('change', () => state.quality = el.value));
    $$('[name="reference"]').forEach(el => el.addEventListener('change', () => state.referenceId = el.value || null));
    $('#reference-select')?.addEventListener('change', e => { state.referenceId=e.target.value||null; renderGenerate(); });
    $('#start-run').addEventListener('click', startRun);
  }
  function referenceRadios() {
    const current = state.referenceId ?? '';
    return `<div class="reference-choice"><label class="radio-tile"><input type="radio" name="reference" value="" ${current === '' ? 'checked' : ''}><span><strong>No reference</strong><small>Use each model’s default voice. BERT always uses its fixed voice.</small></span></label>${state.references.slice(0,1).map(r => `<label class="radio-tile"><input type="radio" name="reference" value="${esc(r.id)}" ${String(current) === String(r.id) ? 'checked' : ''}><span><strong>${esc(r.name)}</strong><small>Transcript source: ${esc(transcriptSourceLabel(r.transcriptSource))} · ${Number(r.seconds || 0).toFixed(1)} sec</small></span></label>`).join('')}</div>${state.references.length > 1 ? `<div class="field" style="margin-top:10px"><label for="reference-select">Choose another reference</label><select class="select" id="reference-select"><option value="">Use no reference</option>${state.references.map(r => `<option value="${esc(r.id)}" ${String(current) === String(r.id) ? 'selected' : ''}>${esc(r.name)} · ${esc(transcriptSourceLabel(r.transcriptSource))} · ${Number(r.seconds || 0).toFixed(1)} sec</option>`).join('')}</select></div>` : ''}<p class="field-help">${state.references.length ? 'Fish and Irodori can use the selected voice sample. BERT uses its fixed voice.' : 'No references yet. You can add a cropped recording in References, or continue without one.'}</p>`;
  }
  async function startRun() {
    const button = $('#start-run'); if (button.disabled || state.generatingRunId) return;
    const hint = $('#submit-hint'); button.disabled = true; hint.textContent = 'Starting your run…';
    try {
      const run = await api('/api/runs', json({ wordKeys: [...state.selected], engines: state.engines || [], referenceId: state.referenceId || null, outputQuality: state.quality || 'OriginalWav' }));
      state.activeRun = run; state.activeRunId = run.id; state.generatingRunId = run.id; nav('runs'); await showRun(run.id); pollRun(run.id);
    } catch (e) { notice(e.message); button.disabled = false; hint.textContent = e.message; }
  }
  async function loadRuns() { const root = $('#view-runs'); if (!root.dataset.loaded) root.innerHTML = `${pageHead('Studio history', 'Runs', 'Generation progress and side-by-side listening live here.')}<div class="loading">Loading runs…</div>`; try { state.runs = await api('/api/runs'); root.dataset.loaded = '1'; renderRuns(); if (state.activeRunId) await showRun(state.activeRunId); } catch (e) { root.innerHTML = `${pageHead('Studio history','Runs','Generation progress and side-by-side listening live here.')}<div class="panel"><div class="empty-state">${esc(e.message)}</div></div>`; } }
  function renderRuns() {
    const root = $('#view-runs'); root.dataset.loaded = '1'; const active = state.activeRunId;
    root.innerHTML = `${pageHead('Studio history', 'Runs', 'Generation progress and side-by-side listening live here.', `<button class="button secondary" id="refresh-runs">Refresh</button>`)}<div id="run-detail"></div><div class="section-title"><h2>All runs</h2><span class="subtle">${state.runs.length} saved</span></div>${state.runs.length ? state.runs.map(r => { const busy=String(state.runActionBusyId)===String(r.id), activeRun=isActiveRun(r), cancellable=activeRun||r.canResume===true; return `<article class="panel run-card"><div><h3>${esc(r.referenceName || 'No reference')} <span class="pill ${statusClass(r.status)}">${esc(r.status)}</span></h3><p>${esc(date(r.createdAt))} · ${Number(r.wordCount)||0} words · ${(r.engines||[]).map(engineName).map(esc).join(', ')} · ${esc(qualityName(r.outputQuality))}</p>${r.progress ? `<p>${esc(r.progress.phase || '')} · ${r.progress.generated||0} generated · ${r.progress.failed||0} failed · ${Math.max(0,(r.progress.total||r.wordCount||0)-(r.progress.generated||0)-(r.progress.failed||0))} remaining</p>` : ''}</div><div class="run-card-actions">${active && String(active) === String(r.id) ? '<span class="pill running">Open</span>' : ''}<button class="button secondary small" data-open-run="${esc(r.id)}">${terminal(r.status) ? 'Compare audio' : 'View progress'}</button>${activeRun && String(r.status).toLowerCase()!=='pausing' ? `<button class="button secondary small" data-run-action="pause" data-run-id="${esc(r.id)}" ${busy?'disabled':''}>${busy?'Working…':'Pause'}</button>` : ''}${(r.canResume===true && !activeRun) ? `<button class="button secondary small" data-run-action="resume" data-run-id="${esc(r.id)}" ${busy||!!state.generatingRunId?'disabled':''}>${busy?'Working…':'Resume'}</button>` : ''}${activeRun && String(r.status).toLowerCase()==='pausing' ? '<span class="subtle">Pause requested…</span>' : ''}${cancellable ? `<button class="button secondary small danger" data-run-action="cancel" data-run-id="${esc(r.id)}" ${busy||String(state.cancelRequestedRunId)===String(r.id)?'disabled':''}>${String(state.cancelRequestedRunId)===String(r.id)?'Cancelling…':'Cancel'}</button>` : ''}</div></article>`; }).join('') : '<div class="panel"><div class="empty-state">No runs yet. Start with Generate.</div></div>'}`;
    $('#refresh-runs').addEventListener('click', loadRuns); $$('[data-open-run]').forEach(b => b.addEventListener('click', () => { state.activeRunId = b.dataset.openRun; showRun(b.dataset.openRun); })); $$('[data-run-action]').forEach(b => b.addEventListener('click', () => runAction(b.dataset.runId,b.dataset.runAction)));
  }
  const terminal = s => ['completed','partial','cancelled','interrupted','failed'].includes(String(s||'').toLowerCase());
  const isActiveRun = r => r?.isActive === true || (r?.isActive == null && ['queued','generating','checking','pausing'].includes(String(r?.status||'').toLowerCase()));
  const canExportRun = r => ['completed','partial','cancelled','failed'].includes(String(r?.status||'').toLowerCase());
  const statusClass = s => /completed|success/i.test(s) ? 'success' : /fail|error/i.test(s) ? 'failed' : /cancel|interrupt/i.test(s) ? '' : 'running';
  function statusText(s) { return String(s || 'unknown').replace(/([a-z])([A-Z])/g,'$1 $2'); }
  function engineName(id) { return engines.find(e => e.id === id)?.name || id || 'Model'; }
  function qualityName(id) { return quality.find(q => q.id === id)?.name || id || 'Audio'; }
  function transcriptSourceLabel(source) {
    if (source === 'User edited') return 'User edited';
    if (source === 'AI library Whisper small; not human-verified') return 'AI library Whisper small; not human-verified';
    if (source === 'Local Whisper small; not human-verified' || source === 'auto') return 'Local Whisper small; not human-verified';
    return source || 'Transcript source unavailable';
  }
  function date(raw) { if (!raw) return 'Date unavailable'; const d = new Date(raw); return Number.isNaN(+d) ? String(raw) : d.toLocaleString(); }
  async function showRun(id) {
    try { const data = await api(`/api/runs/${encodeURIComponent(id)}`); state.activeRun = data; if (isActiveRun(data)) { state.generatingRunId = data.id; pollRun(data.id); } else if(String(state.generatingRunId)===String(data.id)) state.generatingRunId=null; const host = $('#run-detail'); if (!host) return; host.innerHTML = runDetailMarkup(data); bindRunDetail(data); }
    catch (e) { notice(e.message); }
  }
  function runDetailMarkup(r) {
    const p = r.progress || {}, pct = progressPercent(r);
    const checksRemaining = Math.max(0, (p.generated || 0) - (p.checksCompleted || 0));
    const progressText = String(r.status).toLowerCase() === 'checking'
      ? `${p.checksCompleted || 0}/${p.generated || 0} checks completed · ${checksRemaining} remaining`
      : `${p.generated||0} generated · ${p.failed||0} failed · ${p.remaining ?? '…'} remaining`;
    const notes=(r.notes||[]).filter(Boolean);
    const running=isActiveRun(r), paused=String(r.status).toLowerCase()==='paused', pausing=String(r.status).toLowerCase()==='pausing', busy=String(state.runActionBusyId)===String(r.id);
    const progressMeta=canExportRun(r) ? `<div class="progress-meta"><span>Generation finished · ${p.generated||0} clips generated · ${p.failed||0} failed</span><span>${p.checksCompleted||0}/${p.generated||0} checks completed</span></div>` : `<div class="progress-meta"><span>${paused?'Paused':pausing?'Pausing after the current operation…':esc(p.phase || 'Preparing models…')}</span><span>${progressText}</span></div><div class="run-progress" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"><span style="width:${pct}%"></span></div>`;
    const controls=running||r.canResume===true ? `<div class="toolbar">${running&&!pausing?`<button class="button secondary small" id="pause-current" ${busy?'disabled':''}>${busy?'Working…':'Pause generation'}</button>`:''}${r.canResume===true&&!running?`<button class="button secondary small" id="resume-current" ${busy||!!state.generatingRunId?'disabled':''}>${busy?'Working…':'Resume generation'}</button>`:''}${pausing?'<span class="subtle">Pause requested. The current clip or transcript check will finish first.</span>':''}<button class="button secondary small danger" id="cancel-current" ${busy||String(state.cancelRequestedRunId)===String(r.id)?'disabled':''}>${String(state.cancelRequestedRunId)===String(r.id)?'Cancelling…':'Cancel run'}</button><span class="subtle">Progress is saved as it proceeds.</span></div>` : '';
    return `<div class="panel"><div class="panel-inner"><div class="panel-head"><div><p class="eyebrow">Selected run</p><h2>${esc(r.referenceName || 'No reference')} · ${esc(date(r.createdAt))}</h2><p>${(r.engines||[]).map(engineName).map(esc).join(' · ')} · ${esc(qualityName(r.outputQuality))}</p></div><span class="pill ${statusClass(r.status)}">${esc(statusText(r.status))}</span></div>${notes.length?`<div class="clip-detail"><strong>Run notes</strong><ul>${notes.map(note=>`<li>${esc(note)}</li>`).join('')}</ul></div>`:''}${progressMeta}${controls}</div></div>${r.words?.length ? `<div class="section-title"><h2>Compare and choose audio</h2><span class="subtle">Choose one model per word, or leave it out of the export.</span></div><div class="panel comparison">${comparisonTable(r)}</div><div class="form-actions"><span class="muted">Your choices save automatically.</span><button class="button primary" id="export-run" ${canExportRun(r) && !hasPendingChoiceSaves(r.id) ? '' : 'disabled'}>Export selected audio →</button></div>` : ''}`;
  }
  function progressPercent(run) {
    const p=run.progress||{};
    if(canExportRun(run)) return 100;
    if(['paused','interrupted'].includes(String(run.status).toLowerCase())) return p.total ? Math.min(90,Math.round(((p.generated||0)+(p.failed||0))/p.total*90)) : 0;
    if(String(run.status).toLowerCase()==='checking') return 90+Math.round(10*Math.min(1,(p.checksCompleted||0)/Math.max(1,p.generated||0)));
    return p.total ? Math.min(90,Math.round(((p.generated||0)+(p.failed||0))/p.total*90)) : 0;
  }
  function comparisonTable(r, exportedMode = false) {
    const clips = r.clips || [], words = r.words || [], models = r.engines || [...new Set(clips.map(c => c.engine))];
    return `<table class="compare-table"><thead><tr><th>Word</th>${models.map(m=>`<th>${esc(engineName(m))}</th>`).join('')}<th>${exportedMode?'Exported':'Export choice'}</th></tr></thead><tbody>${words.map(w=>{ const k=wordKey(w), selected=r.selections?.[k] || null; return `<tr><td class="compare-word"><div class="word-jp">${esc(w.japanese)}</div><div class="word-reading">${esc(w.kana)}${w.spokenKana && w.spokenKana !== w.kana ? `<div class="word-source">Spoken as ${esc(w.spokenKana)}</div>` : ''}</div><div class="word-meaning">${esc(w.english)}</div><div class="word-source">${esc(w.group)}</div>${w.speechNote?`<div class="clip-detail">${esc(w.speechNote)}</div>`:''}</td>${models.map(m=>{const c=clips.find(x=>x.key===k&&x.engine===m);return `<td class="clip-cell">${clipMarkup(c)}</td>`}).join('')}<td>${exportedMode ? `<span class="pill success">${esc(engineName(selected))}</span>` : `<div class="choice-row">${models.map(m=>{const c=clips.find(x=>x.key===k&&x.engine===m), disabled=!terminal(r.status)||!canChooseClip(c);return `<label class="choice"><input type="radio" name="choice-${esc(k)}" data-choice-key="${esc(k)}" value="${esc(m)}" ${selected===m?'checked':''} ${disabled?'disabled':''}>Use ${esc(engineName(m))}</label>`}).join('')}<label class="choice"><input type="radio" name="choice-${esc(k)}" data-choice-key="${esc(k)}" value="dont-use" ${selected==='dont-use'?'checked':''} ${!terminal(r.status)?'disabled':''}>Don’t export this word</label></div>`}</td></tr>`;}).join('')}</tbody></table>`;
  }
  function canChooseClip(clip) { return !!clip && !!safeUrl(clip.audioUrl) && !/fail|error|cancel|pending|generating/i.test(String(clip.status || '')); }
  function clipMarkup(c) {
    if (!c) return '<span class="subtle">No clip returned</span>';
    if (c.status && !/complete|success|ready|generated/i.test(c.status)) return `<span class="pill ${statusClass(c.status)}">${esc(statusText(c.status))}</span>${c.error?`<div class="clip-detail">${esc(c.error)}</div>`:''}`;
    const audio = safeUrl(c.audioUrl), raw = safeUrl(c.rawUrl);
    return `${audio?`<audio controls preload="none" src="${esc(audio)}" aria-label="Generated audio"></audio>`:'<span class="subtle">Audio unavailable</span>'}<div class="clip-actions">${raw?`<a href="${esc(raw)}" target="_blank" rel="noopener">Raw audio</a>`:''}${c.seconds?`<span>${Number(c.seconds).toFixed(1)} sec</span>`:''}${c.pitchSource?`<span class="pitch-mark">${esc(c.pitchSource)}</span>`:''}</div>${c.transcript?`<div class="clip-detail"><strong>Transcript</strong><br>${esc(c.transcript)}</div>`:''}${c.validation?`<div class="clip-detail"><strong>Check</strong><br>${esc(typeof c.validation==='string'?c.validation:JSON.stringify(c.validation))}</div>`:''}${c.error?`<div class="clip-detail">${esc(c.error)}</div>`:''}`;
  }
  function bindRunDetail(r) {
    $('#cancel-current')?.addEventListener('click', () => cancelRun(r.id));
    $('#pause-current')?.addEventListener('click', () => runAction(r.id,'pause'));
    $('#resume-current')?.addEventListener('click', () => runAction(r.id,'resume'));
    $$('[data-choice-key]').forEach(el => el.addEventListener('change', () => saveChoice(r.id, el.dataset.choiceKey, el.value)));
    $('#export-run')?.addEventListener('click', () => exportRun(r.id, false));
  }
  function hasPendingChoiceSaves(id) { return [...state.pendingChoiceSaves.keys()].some(k => k.startsWith(`${id}:`)); }
  function syncExportButton(id) { const button=$('#export-run'); if(button) button.disabled=!canExportRun(state.activeRun)||hasPendingChoiceSaves(id)||!!state.exportingRun; }
  function saveChoice(id, key, value) {
    const queueKey=`${id}:${key}`, previous=state.pendingChoiceSaves.get(queueKey)||Promise.resolve();
    const save=previous.catch(()=>{}).then(()=>api(`/api/runs/${encodeURIComponent(id)}/selections`, { method:'PUT', headers:{...studioHeaders,'Content-Type':'application/json'}, body:JSON.stringify({ choices:{ [key]: value === 'dont-use' ? 'dont-use' : value } }) }));
    state.pendingChoiceSaves.set(queueKey,save); syncExportButton(id);
    save.catch(e=>{if(state.pendingChoiceSaves.get(queueKey)===save){notice(`Could not save choice: ${e.message}`);showRun(id);}}).finally(()=>{
      if(state.pendingChoiceSaves.get(queueKey)===save) state.pendingChoiceSaves.delete(queueKey);
      syncExportButton(id);
    });
    return save;
  }
  async function cancelRun(id) {
    if(String(state.cancelRequestedRunId)===String(id)||String(state.runActionBusyId)===String(id))return;
    state.cancelRequestedRunId=id; state.runActionBusyId=id; state.activeRunId=id;
    try {
      await api(`/api/runs/${encodeURIComponent(id)}/cancel`, { method:'POST', headers:studioHeaders });
      notice('Cancellation requested.');
      await refreshRunAfterAction(id);
    } catch(e) {
      notice(e.message);
    } finally {
      state.cancelRequestedRunId=null; state.runActionBusyId=null;
      await refreshRunAfterAction(id);
    }
  }
  async function runAction(id, action) {
    if(String(state.runActionBusyId)===String(id) || (action==='resume'&&state.generatingRunId))return;
    state.runActionBusyId=id;
    if(action==='resume'){state.activeRunId=id;state.generatingRunId=id;}
    if(state.view==='runs'){renderRuns();if(action!=='resume'&&String(state.activeRunId)===String(id))await showRun(id);}
    try {
      await api(`/api/runs/${encodeURIComponent(id)}/${action}`,{method:'POST',headers:studioHeaders});
      if(action==='pause') notice('Pause requested. The current operation will finish before the run pauses.',true);
      if(action==='resume') notice('Run resumed.',true);
      await refreshRunAfterAction(id);
    } catch(e) {
      notice(e.message);
      await refreshRunAfterAction(id);
    } finally {
      state.runActionBusyId=null;
      await refreshRunAfterAction(id);
    }
  }
  async function refreshRunAfterAction(id) {
    if(state.view==='runs') await loadRuns();
    else if(String(state.activeRunId)===String(id)) await showRun(id);
  }
  function pollRun(id) {
    if (state.runTimer && String(state.pollingRunId) === String(id)) return;
    clearInterval(state.runTimer); state.pollingRunId=id;
    state.runTimer = setInterval(async () => {
      if (state.pollBusy) return; state.pollBusy=true;
      try {
        const d=await api(`/api/runs/${encodeURIComponent(state.pollingRunId)}`); state.activeRun=d;
        if (terminal(d.status) || String(d.status).toLowerCase()==='paused') { if(String(state.generatingRunId)===String(d.id))state.generatingRunId=null; if(String(state.cancelRequestedRunId)===String(d.id))state.cancelRequestedRunId=null; clearInterval(state.runTimer); state.runTimer=null; state.pollingRunId=null; await loadRuns(); if(String(state.activeRunId)===String(d.id)) await showRun(d.id); if(state.view==='generate') renderGenerate(); }
        else {
          if(isActiveRun(d))state.generatingRunId=d.id;else if(String(state.generatingRunId)===String(d.id))state.generatingRunId=null;
          if(state.view==='runs' && String(state.activeRunId)===String(d.id)) {
            const box=$('#run-detail');
            if(box) {
              const current=$('.progress-meta',box), p=d.progress||{};
              if(current) { const spans=$$('span',current); if(spans[0]) spans[0].textContent=String(d.status).toLowerCase()==='pausing'?'Pausing after the current operation…':p.phase||'Generating audio…'; if(spans[1]) spans[1].textContent=String(d.status).toLowerCase()==='checking' ? `${p.checksCompleted||0}/${p.generated||0} checks completed · ${Math.max(0,(p.generated||0)-(p.checksCompleted||0))} remaining` : `${p.generated||0} generated · ${p.failed||0} failed · ${p.remaining??'…'} remaining`; }
              const bar=$('.run-progress span',box); if(bar) bar.style.width=`${progressPercent(d)}%`;
              if(String(d.status).toLowerCase()==='pausing') await showRun(d.id);
            }
          }
        }
      } catch(e) { clearInterval(state.runTimer); state.runTimer=null; state.pollingRunId=null; notice(`Run status could not be refreshed: ${e.message}`); }
      finally { state.pollBusy=false; }
    }, 2000);
  }
  async function exportRun(id, acknowledged) {
    const button=$('#export-run'); if(state.exportingRun || hasPendingChoiceSaves(id)) return;
    state.exportingRun=id; syncExportButton(id);
    try {
      const r=await api(`/api/runs/${encodeURIComponent(id)}/export`,json({acknowledgeUnselected:acknowledged}));
      notice(`Exported ${r.exportedCount} audio file${r.exportedCount===1?'':'s'}.${r.unselectedCount?` ${r.unselectedCount} unselected word${r.unselectedCount===1?'':'s'} were skipped.`:''}`,true);
      await Promise.all([loadExports(), api('/api/catalog').then(catalog=>{state.catalog=catalog;state.catalogLoaded=true;})]);
      if(state.view==='generate') renderGenerate();
      if(state.view==='runs') await loadRuns();
    } catch(e) {
      if(e.status===409 && e.data?.requiresAcknowledgement) {
        const n=e.data.unselectedCount||0; $('#confirm-copy').textContent=`${n} word${n===1?' was':'s were'} left without a model choice. Those words will be skipped. Continue with export?`;
        const dialog=$('#confirm-dialog'); dialog.showModal(); $('#confirm-export').onclick=ev=>{ev.preventDefault();dialog.close();exportRun(id,true);};
      } else notice(e.message);
    } finally { state.exportingRun=null; if(button) syncExportButton(id); }
  }
  async function loadExports() { const root=$('#view-exported'); if(!root.dataset.loaded) root.innerHTML=`${pageHead('Your library','Exported audio','Listen to the audio you have chosen to keep.')}<div class="loading">Loading exports…</div>`; try { state.exports=await api('/api/exports'); root.dataset.loaded='1'; renderExports(); } catch(e) { root.innerHTML=`${pageHead('Your library','Exported audio','Listen to the audio you have chosen to keep.')}<div class="panel"><div class="empty-state">${esc(e.message)}</div></div>`; } }
  function renderExports() { const root=$('#view-exported'), q=state.exportFilter.toLocaleLowerCase(); const list=state.exports.filter(x=>(state.exportEngine==='all'||String(x.engine).toLowerCase()===state.exportEngine)&&(!q||[x.word?.japanese,x.word?.kana,x.word?.english,x.word?.group,x.engine].some(v=>String(v||'').toLocaleLowerCase().includes(q)))); root.dataset.loaded='1'; root.innerHTML=`${pageHead('Your library','Exported audio','Listen to the audio you have chosen to keep.',`<button id="refresh-exports" class="button secondary">Refresh</button>`)}<div class="filter-row"><label class="search-wrap"><span class="search-icon">⌕</span><input class="input" id="export-search" type="search" placeholder="Search exported words…" value="${esc(state.exportFilter)}" aria-label="Search exported audio"></label><label class="field">Model<select class="select" id="export-model"><option value="all" ${state.exportEngine==='all'?'selected':''}>All models</option>${engines.map(m=>`<option value="${m.id}" ${state.exportEngine===m.id?'selected':''}>${m.name}</option>`).join('')}</select></label><span class="word-count">${list.length} of ${state.exports.length} exports</span></div>${list.length?`<div class="panel comparison"><table class="compare-table"><thead><tr><th>Word</th><th>Selected model</th><th>Audio</th><th>Run</th><th>Notes</th></tr></thead><tbody>${list.map(x=>{const w=x.word||{};const audio=safeUrl(x.audioUrl);return `<tr><td class="compare-word"><div class="word-jp">${esc(w.japanese)}</div><div class="word-reading">${esc(w.kana)}${w.spokenKana && w.spokenKana !== w.kana ? `<div class="word-source">Spoken as ${esc(w.spokenKana)}</div>` : ''}</div><div class="word-meaning">${esc(w.english)}</div><div class="word-source">${esc(w.group)}</div>${w.speechNote?`<div class="clip-detail">${esc(w.speechNote)}</div>`:''}</td><td>${esc(engineName(x.engine))}<div class="word-source">${esc(qualityName(x.outputQuality))}</div></td><td class="clip-cell">${audio?`<audio controls preload="none" src="${esc(audio)}" aria-label="Exported audio"></audio>`:'<span class="subtle">Audio URL unavailable</span>'}${x.transcript?`<div class="clip-detail"><strong>Transcript</strong><br>${esc(x.transcript)}</div>`:''}</td><td class="word-source">${esc(date(x.exportedAt))}<br><button class="button text small" data-run="${esc(x.runId)}">Open run</button></td><td class="clip-detail">${esc(typeof x.validation==='string'?x.validation:(x.validation?JSON.stringify(x.validation):''))}</td></tr>`;}).join('')}</tbody></table></div>`:'<div class="panel"><div class="empty-state">No exported audio matches these filters.</div></div>'}`; $('#refresh-exports').addEventListener('click',loadExports); $('#export-search').addEventListener('input',e=>{state.exportFilter=e.target.value;const n=e.target.selectionStart;renderExports();$('#export-search').focus();$('#export-search').setSelectionRange(n,n);}); $('#export-model').addEventListener('change',e=>{state.exportEngine=e.target.value;renderExports();}); $$('[data-run]').forEach(b=>b.addEventListener('click',()=>{state.activeRunId=b.dataset.run;nav('runs');})); }
  async function loadReferences() { const root=$('#view-references'); if(!root.dataset.loaded) root.innerHTML=`${pageHead('Voice library','References','Prepare short voice samples for reference-conditioned models.')}<div class="loading">Loading references…</div>`; try { state.references=await api('/api/references'); root.dataset.loaded='1'; renderReferences(); } catch(e) { root.innerHTML=`${pageHead('Voice library','References','Prepare short voice samples for reference-conditioned models.')}<div class="panel"><div class="empty-state">${esc(e.message)}</div></div>`; } }
  function renderReferences() { const root=$('#view-references'), q=state.referenceFilter.toLocaleLowerCase(); const list=state.references.filter(r=>!q||[r.name,r.transcript,r.sourceName].some(x=>String(x||'').toLocaleLowerCase().includes(q))); root.dataset.loaded='1'; root.innerHTML=`${pageHead('Voice library','References','Prepare short voice samples for reference-conditioned models.')}<form id="reference-form" class="panel reference-form"><h2>Add a reference recording</h2><p>New references use AI library Whisper small for an automatic transcript. It is not human-verified; review it before use.</p><div class="form-row"><div class="field"><label for="ref-name">Reference name</label><input class="input" id="ref-name" name="name" required maxlength="80" placeholder="For example, Calm reading voice"></div><div class="field"><label for="ref-file">Audio recording</label><div class="file-drop"><input id="ref-file" name="file" type="file" accept="audio/*,.wav,.mp3,.m4a,.webm" required><div class="field-help" id="file-info">Choose an audio file to preview and select a crop.</div></div></div></div><div id="crop-controls" hidden style="margin-top:14px"><div class="field"><label>Preview recording and choose a crop</label><audio id="source-preview" class="crop-player" controls></audio></div><div class="crop-times"><div class="field"><label for="crop-start">Start (seconds)</label><input class="input" id="crop-start" type="number" min="0" step="0.1" value="0"></div><div class="field"><label for="crop-duration">Length (seconds, 1–30)</label><input class="input" id="crop-duration" type="number" min="1" max="30" step="0.1" value="10"></div></div></div><div class="form-actions"><span class="field-help">No recording is made in the browser; this uploads your chosen audio to the local studio.</span><button class="button primary" id="create-reference" type="submit">Prepare reference</button></div></form><div class="section-title"><h2>Prepared references</h2><div class="toolbar"><label class="search-wrap"><span class="search-icon">⌕</span><input class="input" id="reference-search" type="search" placeholder="Search references…" value="${esc(state.referenceFilter)}" aria-label="Search references"></label><span class="word-count">${list.length} of ${state.references.length}</span></div></div>${list.length?list.map(r=>referenceCard(r)).join(''):'<div class="panel"><div class="empty-state">No references yet. Add a cropped recording above.</div></div>'}`;
    $('#reference-search').addEventListener('input',e=>{state.referenceFilter=e.target.value;const n=e.target.selectionStart;renderReferences();$('#reference-search').focus();$('#reference-search').setSelectionRange(n,n);});
    $('#ref-file').addEventListener('change',e=>{
      const file=e.target.files?.[0];if(!file)return;
      const url=URL.createObjectURL(file),player=$('#source-preview');
      if(state.previewUrl)URL.revokeObjectURL(state.previewUrl);
      state.previewUrl=url;state.previewFile=file;state.cropStartEdited=false;state.cropDurationEdited=false;
      $('#crop-start').value='0';$('#crop-duration').value='10';
      player.src=url;$('#crop-controls').hidden=false;$('#file-info').textContent=`${file.name} · ${(file.size/1024/1024).toFixed(1)} MB`;
      player.onloadedmetadata=()=>{
        if(state.previewFile!==file)return;
        const duration=player.duration;
        if(!Number.isFinite(duration)||duration<=0)return;
        const durationInput=$('#crop-duration'),startInput=$('#crop-start'),maxDuration=Math.min(30,duration);
        if(!state.cropStartEdited)startInput.value='0';
        if(!state.cropDurationEdited)durationInput.value=Math.min(30,Math.max(1,duration)).toFixed(1);
        if(maxDuration>=1)durationInput.max=String(maxDuration);else durationInput.removeAttribute('max');
        const selectedDuration=Number(durationInput.value);
        if(Number.isFinite(selectedDuration)&&selectedDuration>=0)startInput.max=String(Math.max(0,duration-selectedDuration));
      };
    });
    $('#crop-start').addEventListener('input',()=>{state.cropStartEdited=true;const p=$('#source-preview'),start=Number($('#crop-start').value);if(Number.isFinite(start)&&start>=0&&Number.isFinite(p.duration)&&start<p.duration)p.currentTime=start;});
    $('#crop-duration').addEventListener('input',()=>{state.cropDurationEdited=true;const p=$('#source-preview'),duration=Number($('#crop-duration').value);if(Number.isFinite(p.duration)&&p.duration>0&&Number.isFinite(duration)&&duration>=0)$('#crop-start').max=String(Math.max(0,p.duration-duration));});
    $('#reference-form').addEventListener('submit',createReference);
    $$('[data-edit-ref]').forEach(b=>b.addEventListener('click',()=>editReference(b.dataset.editRef)));
  }
  function referenceCard(r) { const url=safeUrl(r.mediaUrl), source=transcriptSourceLabel(r.transcriptSource); return `<article class="panel reference-card"><div><h3>${esc(r.name)}</h3><p>${esc(r.sourceName||'Local recording')} · ${Number(r.seconds||0).toFixed(1)} sec</p><p>${esc(date(r.createdAt))}</p>${source!=='User edited'?`<span class="pill running">${esc(source)}</span>`:''}</div><div>${url?`<audio controls preload="none" src="${esc(url)}" aria-label="Reference audio"></audio>`:'<span class="subtle">Preview unavailable</span>'}<div class="reference-transcript">${esc(r.transcript||'No transcript yet.')}</div><p class="subtle">Transcript source: ${esc(source)}</p></div><div><button class="button secondary small" data-edit-ref="${esc(r.id)}">Edit name and transcript</button></div></article>`; }
  async function createReference(ev) { ev.preventDefault();const form=ev.currentTarget,file=$('#ref-file').files?.[0],name=$('#ref-name').value.trim(),start=Number($('#crop-start').value),duration=Number($('#crop-duration').value),btn=$('#create-reference');if(!file||!name||duration<1||duration>30||start<0){notice('Enter a name, choose an audio file and set a crop from 1 to 30 seconds.');return;}btn.disabled=true;btn.textContent='Preparing reference…';try{const data=new FormData();data.append('name',name);data.append('file',file);data.append('startSeconds',String(start));data.append('durationSeconds',String(duration));await api('/api/references',{method:'POST',headers:studioHeaders,body:data});if(state.previewUrl)URL.revokeObjectURL(state.previewUrl);state.previewUrl=null;notice('Reference prepared. Review its transcript before using it.',true);await loadReferences();}catch(e){notice(e.message);btn.disabled=false;btn.textContent='Prepare reference';} }
  async function editReference(id) { const r=state.references.find(x=>String(x.id)===String(id));if(!r)return;const name=prompt('Reference name',r.name);if(name===null)return;const transcript=prompt('Review or correct the transcript',r.transcript||'');if(transcript===null)return;try{await api(`/api/references/${encodeURIComponent(id)}`,{method:'PUT',headers:{...studioHeaders,'Content-Type':'application/json'},body:JSON.stringify({name:name.trim(),transcript})});notice('Reference details saved.',true);await loadReferences();}catch(e){notice(e.message);} }
  async function init() {
    $$('.nav-item').forEach(b=>b.addEventListener('click',()=>nav(b.dataset.view)));
    window.addEventListener('hashchange',()=>{const view=location.hash.slice(1);if(['generate','runs','exported','references'].includes(view)&&view!==state.view)nav(view);});
    $('#mobile-menu').addEventListener('click',()=>{const open=$('.sidebar').classList.toggle('open');$('#mobile-menu').setAttribute('aria-expanded',String(open));});
    const initial=location.hash.slice(1);if(['generate','runs','exported','references'].includes(initial))state.view=initial;
    state.groups=new Set(groups);state.engines=['bert'];state.quality='OriginalWav';
    try { const health=await api('/api/health'); $('#health-dot').classList.add('ready');$('#health-label').textContent=health?.ready?'Studio ready':'Studio connected'; if(health?.activeRunId){state.generatingRunId=health.activeRunId;state.activeRunId=health.activeRunId;state.view='runs';} }
    catch { $('#health-dot').classList.add('error');$('#health-label').textContent='Studio unavailable'; }
    try { const [catalog,refs]=await Promise.all([api('/api/catalog'),api('/api/references')]);state.catalog=catalog;state.catalogLoaded=true;state.references=refs; if(state.view==='references')renderReferences(); else if(state.view==='runs')await loadRuns();else if(state.view==='exported')await loadExports();else renderGenerate(); }
    catch(e) { state.catalogLoaded=true; $('#view-generate').innerHTML=`${pageHead('Audio workspace','Generate audio','Choose vocabulary, models and a voice reference for your next recording run.')}<div class="panel"><div class="empty-state">Could not load the studio catalog: ${esc(e.message)}<br><button class="button secondary" id="retry-load" style="margin-top:14px">Try again</button></div></div>`;$('#retry-load').addEventListener('click',init); }
    if(state.view!=='generate') { $$('.view').forEach(el=>el.hidden=el.id!==`view-${state.view}`); $$('.nav-item').forEach(el=>el.classList.toggle('active',el.dataset.view===state.view)); history.replaceState(null,'',`#${state.view}`); }
  }
  init();
})();
