/*
 * roadmap-spa.js — Roadmap admin SPA bundle.
 *
 * Generated from docs/roadmap-presentation-editable.html by _extract_spa_js.js.
 * In admin mode (window.RoadmapAdmin present) this file talks to the server;
 * otherwise it falls back to the original localStorage behaviour.
 *
 * Re-run the extractor whenever the source HTML's logic is updated.
 */

/* ============ ADMIN ADAPTER ============
 * When this script runs inside the admin Edit page, window.RoadmapAdmin is
 * present (provided by Edit.cshtml). The adapter swaps localStorage I/O for
 * server I/O and disables the GitHub sync.
 * When run from the standalone docs/ HTML, RoadmapAdmin is undefined and the
 * original localStorage behaviour kicks in.
 * ====================================== */
const ADMIN_MODE = !!(typeof window !== 'undefined' && window.RoadmapAdmin && window.RoadmapAdmin.initial);
let _saveTimer = null;
let _savePending = false;
const SAVE_DEBOUNCE_MS = 800;


/* ============ DATA ============ */
const STORAGE_KEY = 'asps_roadmap_v2';

const STAGES = {
  now:{label:'לפני Angel', colCls:'now-col', cellCls:'active-now'},
  angel:{label:'Angel', colCls:'angel-col', cellCls:'active-angel'},
  vc:{label:'VC', colCls:'vc-col', cellCls:'active-vc'},
  gap:{label:'לא מסווג', colCls:'', cellCls:'gap-cell'},
};
const STATUSES = {todo:'לא התחיל', prog:'בביצוע', done:'הושלם', block:'חסום'};
const PRIORITIES = {high:'גבוהה', med:'בינונית', low:'נמוכה'};
const TRACKS = {
  product:{label:'Product', ico:'🛠️', sub:'פיצ\'רים טכניים — CTO/Dev Lead', cls:'track-product'},
  compliance:{label:'Compliance', ico:'🔐', sub:'ISO, SOC2, GDPR, Legal — CEO/COO', cls:'track-compliance'},
  gtm:{label:'Go-to-Market', ico:'📣', sub:'שיווק, PR, Education — CEO/BD', cls:'track-gtm'},
  hiring:{label:'Hiring', ico:'👤', sub:'תפקידים וגיוס — CEO', cls:'track-hiring'},
};

/* Each row = a distinct task title in a category. stage determines which column it appears in. */
const SEED = [
  // Customers
  ['רישום לקוח, מכשירים, מעקב ולוח בקרה','👥 ניהול לקוחות ומוטבים','now','done','high','product','M'],
  ['פורטל לקוחות','👥 ניהול לקוחות ומוטבים','now','prog','high','product','M'],
  ['שדרוג פורטל לקוחות','👥 ניהול לקוחות ומוטבים','angel','todo','med','product','M'],
  ['Keycloak + Identity Providers','👥 ניהול לקוחות ומוטבים','angel','todo','med','product','M'],
  ['תשתיות חיוב לקוחות','👥 ניהול לקוחות ומוטבים','gap','todo','high','product','L','','לא מסווג — Now או Angel?'],

  // Fraud
  ['Tech Support Scam','🔍 אנליזה - זיהוי תרחישי הונאה','now','done','high','product','M'],
  ['Investment Scam','🔍 אנליזה - זיהוי תרחישי הונאה','now','done','high','product','M'],
  ['תרחישי הונאה נוספים','🔍 אנליזה - זיהוי תרחישי הונאה','gap','todo','med','product','','','לפרט איזה תרחישים'],

  // Danger
  ['Remote Access + Sensitive Website','⚠️ זיהוי סכנה מיידית וסריקות','now','done','high','product','S'],
  ['סריקת SMS','⚠️ זיהוי סכנה מיידית וסריקות','now','done','high','product','M'],
  ['סריקת Email','⚠️ זיהוי סכנה מיידית וסריקות','now','prog','high','product','M'],
  ['סריקת WhatsApp','⚠️ זיהוי סכנה מיידית וסריקות','angel','todo','high','product','L'],
  ['זיהוי מספר טלפון חד-פעמי / ידוע','⚠️ זיהוי סכנה מיידית וסריקות','now','done','med','product','S'],
  ['זיהוי קול סינתטי','⚠️ זיהוי סכנה מיידית וסריקות','vc','todo','med','product','XL','','R&D או רכישת API?'],
  ['מצבי סכנה מיידית נוספים','⚠️ זיהוי סכנה מיידית וסריקות','gap','todo','low','product','','','לפרט'],

  // URLs
  ['השלמת טיפול ב-TrackUrlAlert','🌐 Tracked URLs & Domains','gap','prog','med','product','S','','כנראה Now — כבר בפיתוח'],
  ['ניהול Tracked URLs','🌐 Tracked URLs & Domains','gap','todo','med','product','M'],
  ['הפצת דומיין מסוכן בין לקוחות','🌐 Tracked URLs & Domains','gap','todo','high','product','L','','Network Effect חזק!'],

  // Escalation
  ['ניהול העדפות לקוח (Risk Score שונים)','🔔 ניהול הסלמה והתראות','now','done','med','product','S'],
  ['Protective Actions - התרעות, SMS','🔔 ניהול הסלמה והתראות','now','prog','high','product','M'],
  ['Protective Actions - שדרוג','🔔 ניהול הסלמה והתראות','angel','todo','med','product','M'],

  // Intel
  ['Blacklisted: Scam','🧠 מודיעין - Intelligence','now','done','high','product','S'],
  ['Blacklisted: Phishing','🧠 מודיעין - Intelligence','now','done','high','product','S'],
  ['Bank Websites','🧠 מודיעין - Intelligence','now','done','med','product','S'],
  ['Scam Websites','🧠 מודיעין - Intelligence','now','done','med','product','S'],
  ['Lead Lists','🧠 מודיעין - Intelligence','now','prog','med','product','M'],
  ['Blacklisted: Phone Numbers','🧠 מודיעין - Intelligence','now','done','med','product','S'],
  ['Data Collaboration Manager','🧠 מודיעין - Intelligence','gap','todo','med','product','L','','לא מוגדר'],

  // Languages
  ['עברית + אנגלית US','🌍 תמיכה בשפות','now','done','high','product','S'],
  ['רוסית, אנגלית UK, צרפתית, גרמנית','🌍 תמיכה בשפות','angel','todo','med','product','M'],
  ['ספרדית, ערבית + נוספות','🌍 תמיכה בשפות','vc','todo','low','product','M'],

  // App
  ['Android - בסיסי','📱 אפליקציה','now','prog','high','product','M'],
  ['Android - מושלם','📱 אפליקציה','angel','todo','high','product','L','','צריך הגדרת "מוכן"'],
  ['iOS','📱 אפליקציה','angel','todo','high','product','L','','iOS = 40% מהשוק בישראל'],
  ['Browser Extension','📱 אפליקציה','vc','todo','med','product','L'],

  // Production
  ['Pipeline: Dev → Automation → Production','🏗️ תשתיות Production','angel','todo','high','product','M'],
  ['מערך QA','🏗️ תשתיות Production','angel','todo','high','product','M'],
  ['Cloud, Backup, Recovery, Scalability','🏗️ תשתיות Production','angel','todo','high','product','L'],
  ['Automation','🏗️ תשתיות Production','angel','todo','med','product','M'],
  ['בדיקות עומס - Load Tests','🏗️ תשתיות Production','vc','todo','med','product','M'],

  // Security
  ['ISO-27001','🔐 אבטחת מידע','angel','todo','high','compliance','XL','','9-12 חודשים'],
  ['SOC 2 Type I','🔐 אבטחת מידע','angel','todo','high','compliance','XL','','6-9 חודשים'],
  ['CCPA','🔐 אבטחת מידע','angel','todo','med','compliance','M'],
  ['GDPR','🔐 אבטחת מידע','angel','todo','high','compliance','M'],
  ['Penetration Tests','🔐 אבטחת מידע','angel','todo','high','compliance','M'],

  // Legal
  ['תשתית משפטית','⚖️ משפטיות Legal','angel','todo','high','compliance','M'],
  ['ביטוח','⚖️ משפטיות Legal','gap','todo','med','compliance','','','לא ברור מתי'],

  // Trust
  ['יח״צ - PR','🤝 בניית Trust','gap','todo','med','gtm','M'],
  ['שירות לקוחות - AI + היברידי','🤝 בניית Trust','angel','todo','med','gtm','L'],
  ['Education Center: Articles, Blog, Movies, TV','🤝 בניית Trust','vc','todo','low','gtm','XL'],
  ['Community Services','🤝 בניית Trust','vc','todo','low','gtm','L'],
  ['Legal Advice (ייעוץ משפטי התנדבותי)','🤝 בניית Trust','gap','todo','low','gtm','M'],

  // BD
  ['שיווק ופרסום','📈 פיתוח עסקי','gap','todo','high','gtm','L'],
  ['B2B Manager','📈 פיתוח עסקי','gap','todo','high','hiring','','','מתי לגייס?'],
  ['NPO Manager','📈 פיתוח עסקי','gap','todo','med','hiring','','','שותפויות עם עמותות'],
];

function seedItems(){
  return SEED.map((r,i)=>({
    id:'i_'+Math.random().toString(36).slice(2,9)+i,
    title:r[0], category:r[1], stage:r[2], status:r[3], priority:r[4],
    track:r[5], effort:r[6]||'', due:r[7]||'', desc:r[8]||'', owner:'',
    order:i, updatedAt:Date.now(),
  }));
}

/* Build the initial categories list from each matrix's hardcoded data-cats.
   Runs at script load (DOM ready since script is at end of body). */
function deriveCategoriesFromHTML(){
  const cats = [];
  document.querySelectorAll('[data-matrix]').forEach((m, i)=>{
    const id = m.dataset.matrixId || ('m'+(i+1));
    if(!m.dataset.matrixId) m.dataset.matrixId = id;
    const list = (m.dataset.cats||'').split(',').map(s=>s.trim()).filter(Boolean);
    list.forEach((label, idx)=>{
      cats.push({label, matrixId:id, order:idx});
    });
  });
  return cats;
}

/* Build the initial slides list from each .slide that contains a [data-matrix]. */
function deriveSlidesFromHTML(){
  return Array.from(document.querySelectorAll('.slide'))
    .filter(s => s.querySelector('[data-matrix]'))
    .map((s, idx) => {
      const matrix = s.querySelector('[data-matrix]');
      const id = matrix.dataset.matrixId || ('m'+(idx+1));
      matrix.dataset.matrixId = id;
      return {
        id,
        num:   s.querySelector('.slide-num')?.textContent?.trim() || '',
        title: s.querySelector('h2')?.textContent?.trim() || '',
        desc:  s.querySelector('.slide-desc')?.textContent?.trim() || '',
        order: idx,
        collapsed: false,
      };
    });
}

function seedState(){
  return {
    items: seedItems(),
    categories: deriveCategoriesFromHTML(),
    slides: deriveSlidesFromHTML(),
  };
}

/* Move/reorder an item: switch its category/stage if needed and place it at
   `position` within the target category (0 = first, length = last).
   Reassigns sequential `order` values across the target category. */
function moveItemToPosition(itemId, targetCategory, targetStage, position){
  const item = state.items.find(i=>i.id===itemId);
  if(!item) return;
  if(targetCategory) item.category = targetCategory;
  if(targetStage)    item.stage    = targetStage;
  item.updatedAt = Date.now();

  const catItems = state.items
    .filter(i=>i.category===targetCategory && i.id!==itemId)
    .sort((a,b)=>(a.order||0)-(b.order||0));
  const pos = Math.max(0, Math.min(position|0, catItems.length));
  catItems.splice(pos, 0, item);
  catItems.forEach((it, idx)=>{ it.order = idx; });
  save();
}

/* ============ SLIDE-LEVEL OPERATIONS ============ */

/* Wraps the static matrix slides in a single container so we can regenerate
   them dynamically. Idempotent — safe to call multiple times. */
function ensureSlidesContainer(){
  let cont = document.getElementById('matrixSlides');
  if(cont) return cont;
  const matrixSlides = Array.from(document.querySelectorAll('.slide'))
    .filter(s => s.querySelector('[data-matrix]'));
  if(matrixSlides.length === 0){
    // No matrix slides at all — still create a placeholder before the next "fixed" slide.
    cont = document.createElement('div');
    cont.id = 'matrixSlides';
    const allSlides = Array.from(document.querySelectorAll('.slide'));
    // Insert after the journey slide (or after first slide as fallback)
    const journey = document.querySelector('.journey-slide');
    const after = journey || allSlides[0];
    if(after && after.parentNode) after.parentNode.insertBefore(cont, after.nextSibling);
    else document.body.appendChild(cont);
    return cont;
  }
  cont = document.createElement('div');
  cont.id = 'matrixSlides';
  matrixSlides[0].parentNode.insertBefore(cont, matrixSlides[0]);
  matrixSlides.forEach(s => s.remove());
  return cont;
}

function renderAllSlides(){
  const cont = ensureSlidesContainer();
  if(!cont) return;
  const slides = [...state.slides].sort((a,b)=>(a.order||0)-(b.order||0));
  cont.innerHTML = slides.map(renderSlide).join('');
  renderAllMatrices();      // fills each matrix div
  attachSlideHandlers();    // wires slide-level toggle/edit/delete/drag
}

function renderSlide(slide){
  const collapsedCls = slide.collapsed ? 'slide-collapsed' : '';
  const catCount = state.categories.filter(c=>c.matrixId===slide.id).length;
  const itemCount = state.items.filter(i=>state.categories.find(c=>c.matrixId===slide.id && c.label===i.category)).length;
  const canDelete = catCount === 0 && itemCount === 0;
  const deleteTitle = canDelete ? 'מחק שקף ריק' : `${catCount} קטגוריות / ${itemCount} פריטים בשקף — לא ניתן למחוק`;
  return `
    <div class="slide ${collapsedCls}" data-slide-id="${esc(slide.id)}">
      <div class="slide-header" data-slide-handle="${esc(slide.id)}">
        <button class="slide-toggle" data-toggle-slide="${esc(slide.id)}" title="כווץ / פתח שקף">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>
        </button>
        <div class="txt">
          <div class="slide-num">${esc(slide.num||'')}</div>
          <h2>${esc(slide.title||'')}</h2>
          <div class="slide-desc">${esc(slide.desc||'')}</div>
        </div>
        <div class="slide-actions">
          <button class="slide-action edit" data-edit-slide="${esc(slide.id)}" title="ערוך שקף">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 1 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>
          </button>
          <button class="slide-action delete ${canDelete?'':'disabled'}" data-delete-slide="${esc(slide.id)}" title="${esc(deleteTitle)}" ${canDelete?'':'aria-disabled="true"'}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-2 14a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2L5 6"/></svg>
          </button>
        </div>
      </div>
      <div class="slide-body">
        <div class="matrix" data-matrix="" data-matrix-id="${esc(slide.id)}"></div>
      </div>
    </div>`;
}

/* ============ SLIDE MODAL ============ */
let slideEditingId = null;

function openSlideModal(slideId){
  slideEditingId = slideId || null;
  const isEdit = !!slideId;
  let s = isEdit ? state.slides.find(x=>x.id===slideId) : {num:'',title:'',desc:''};
  if(isEdit && !s){toast('שקף לא נמצא');slideEditingId=null;return}
  $('#fSlideNum').value   = s.num   || '';
  $('#fSlideTitle').value = s.title || '';
  $('#fSlideDesc').value  = s.desc  || '';
  $('#modalSlideTitle').textContent = isEdit ? 'עריכת שקף' : 'שקף חדש';
  $('#modalSlideSave').textContent  = isEdit ? 'שמירה' : 'הוסף';
  // Delete button: only for edit, and only if empty
  const catCount  = isEdit ? state.categories.filter(c=>c.matrixId===slideId).length : 0;
  $('#modalSlideDelete').style.display = (isEdit && catCount===0) ? 'inline-flex' : 'none';
  $('#modalSlideBg').classList.add('open');
  setTimeout(()=>$('#fSlideTitle').focus(),60);
}
function closeSlideModal(){
  $('#modalSlideBg').classList.remove('open');
  slideEditingId = null;
  $('#modalSlideTitle').textContent = 'שקף חדש';
  $('#modalSlideSave').textContent = 'הוסף';
  $('#modalSlideDelete').style.display = 'none';
}
function nextSlideId(){
  const nums = state.slides.map(s => {
    const m = String(s.id||'').match(/^m(\d+)/);
    return m ? parseInt(m[1],10) : 0;
  });
  return 'm' + (Math.max(0, ...nums) + 1);
}
function saveSlideModal(){
  const num = $('#fSlideNum').value.trim();
  const title = $('#fSlideTitle').value.trim();
  const desc = $('#fSlideDesc').value.trim();
  if(!title){$('#fSlideTitle').focus();toast('כותרת חובה');return}

  if(slideEditingId){
    const s = state.slides.find(x=>x.id===slideEditingId);
    if(!s) return;
    s.num = num; s.title = title; s.desc = desc;
    save(); closeSlideModal(); renderAllSlides();
    toast('שקף עודכן');
  }else{
    const id = nextSlideId();
    const order = state.slides.length;
    state.slides.push({id, num, title, desc, order, collapsed:false});
    save(); closeSlideModal(); renderAllSlides();
    toast('שקף נוסף');
    // Scroll to new slide
    const newEl = document.querySelector(`[data-slide-id="${id}"]`);
    if(newEl) newEl.scrollIntoView({behavior:'smooth', block:'start'});
  }
}
function deleteSlide(slideId){
  const s = state.slides.find(x=>x.id===slideId);
  if(!s) return;
  const catCount = state.categories.filter(c=>c.matrixId===slideId).length;
  if(catCount>0){toast(`לא ניתן למחוק — ${catCount} קטגוריות בשקף`); return}
  if(!confirm(`למחוק את השקף "${s.title || s.num || s.id}"?`)) return;
  state.slides = state.slides.filter(x=>x.id!==slideId);
  state.slides.sort((a,b)=>(a.order||0)-(b.order||0)).forEach((x,idx)=>x.order=idx);
  save(); closeSlideModal(); renderAllSlides();
  toast('שקף נמחק');
}
function toggleSlideCollapse(slideId){
  const s = state.slides.find(x=>x.id===slideId);
  if(!s) return;
  s.collapsed = !s.collapsed;
  save();
  // Toggle CSS class only for smoothness
  const slideEl = document.querySelector(`[data-slide-id="${slideId}"]`);
  if(slideEl) slideEl.classList.toggle('slide-collapsed', s.collapsed);
}
function moveSlide(slideId, beforeSlideId, insertAbove){
  const s = state.slides.find(x=>x.id===slideId);
  if(!s) return;
  const others = state.slides.filter(x=>x.id!==slideId)
    .sort((a,b)=>(a.order||0)-(b.order||0));
  let position;
  if(beforeSlideId){
    const idx = others.findIndex(x=>x.id===beforeSlideId);
    position = idx<0 ? others.length : (insertAbove ? idx : idx+1);
  }else{
    position = others.length;
  }
  position = Math.max(0, Math.min(position|0, others.length));
  others.splice(position, 0, s);
  others.forEach((x,idx)=>{x.order = idx});
  save();
}

function attachSlideHandlers(){
  // Toggle collapse
  $$('[data-toggle-slide]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      toggleSlideCollapse(el.dataset.toggleSlide);
    });
  });
  // Edit
  $$('[data-edit-slide]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      openSlideModal(el.dataset.editSlide);
    });
  });
  // Delete
  $$('[data-delete-slide]').forEach(el=>{
    el.addEventListener('click', e=>{
      if(el.classList.contains('disabled')) return;
      e.stopPropagation();
      deleteSlide(el.dataset.deleteSlide);
    });
  });
  // Drag-and-drop slides (handle = slide-header)
  $$('[data-slide-handle]').forEach(handle=>{
    const slideEl = handle.closest('[data-slide-id]');
    if(!slideEl) return;
    handle.draggable = true;
    handle.addEventListener('dragstart', e=>{
      // Don't start drag if user grabbed an inner button/input
      if(e.target.closest('button, input, textarea')) { e.preventDefault(); return; }
      _dragKind = 'slide';
      slideEl.classList.add('dragging-slide');
      e.dataTransfer.setData('text/plain', slideEl.dataset.slideId);
      e.dataTransfer.effectAllowed = 'move';
    });
    handle.addEventListener('dragend', ()=>{
      _dragKind = null;
      slideEl.classList.remove('dragging-slide');
    });

    slideEl.addEventListener('dragover', e=>{
      if(_dragKind!=='slide') return;
      if(slideEl.classList.contains('dragging-slide')) return;
      e.preventDefault();
      const rect = slideEl.getBoundingClientRect();
      const above = e.clientY < rect.top + rect.height/2;
      slideEl.classList.toggle('slide-drop-above', above);
      slideEl.classList.toggle('slide-drop-below', !above);
    });
    slideEl.addEventListener('dragleave', ()=>{
      slideEl.classList.remove('slide-drop-above','slide-drop-below');
    });
    slideEl.addEventListener('drop', e=>{
      if(_dragKind!=='slide') return;
      if(slideEl.classList.contains('dragging-slide')) return;
      e.preventDefault();
      slideEl.classList.remove('slide-drop-above','slide-drop-below');
      const draggedId = e.dataTransfer.getData('text/plain');
      const targetId = slideEl.dataset.slideId;
      if(!draggedId || draggedId===targetId) return;
      const rect = slideEl.getBoundingClientRect();
      const above = e.clientY < rect.top + rect.height/2;
      moveSlide(draggedId, targetId, above);
      renderAllSlides();
      toast('סדר השקפים עודכן');
    });
  });
}

/* Move/reorder a category between matrices (slides) and within a matrix.
   - If `beforeLabel` is provided, drop relative to that target (above or below).
   - If `beforeLabel` is null, drop at end of `targetMatrixId`. */
function moveCategoryToMatrix(label, targetMatrixId, beforeLabel, insertAbove){
  const cat = state.categories.find(c=>c.label===label);
  if(!cat) return;
  cat.matrixId = targetMatrixId;

  const others = state.categories
    .filter(c=>c.matrixId===targetMatrixId && c.label!==label)
    .sort((a,b)=>(a.order||0)-(b.order||0));

  let position;
  if(beforeLabel){
    const targetIdx = others.findIndex(c=>c.label===beforeLabel);
    position = targetIdx<0 ? others.length : (insertAbove ? targetIdx : targetIdx+1);
  }else{
    position = others.length; // end of target matrix
  }
  position = Math.max(0, Math.min(position|0, others.length));
  others.splice(position, 0, cat);
  others.forEach((c, idx)=>{ c.order = idx; });
  save();
}

let state = loadState();
let query = '';
let editingId = null;
let modalDraft = {};

function loadState(){
  if (ADMIN_MODE) {
    // Hydrate state from the JSON blob the server passed via window.RoadmapAdmin.initial.data
    const raw = window.RoadmapAdmin.initial && window.RoadmapAdmin.initial.data;
    let parsed = null;
    try { parsed = (typeof raw === 'string') ? JSON.parse(raw || '{}') : (raw || {}); }
    catch (_) { parsed = {}; }
    if (!Array.isArray(parsed.items))      parsed.items = [];
    if (!Array.isArray(parsed.categories)) parsed.categories = [];
    if (!Array.isArray(parsed.slides))     parsed.slides = [];
    return parsed;
  }
  try{
    const raw = localStorage.getItem(STORAGE_KEY);
    if(!raw) return seedState();
    const p = JSON.parse(raw);
    if(!p.items || !Array.isArray(p.items)) return seedState();
    if(!Array.isArray(p.categories)) p.categories = deriveCategoriesFromHTML();
    if(!Array.isArray(p.slides))     p.slides     = deriveSlidesFromHTML();
    return p;
  }catch(e){return seedState()}
}
function save(){
  if (ADMIN_MODE) {
    // Mark dirty immediately for the UI badge, debounce the actual server POST
    if (window.RoadmapAdmin.markDirty) window.RoadmapAdmin.markDirty();
    _savePending = true;
    clearTimeout(_saveTimer);
    _saveTimer = setTimeout(() => {
      _savePending = false;
      window.RoadmapAdmin.save(JSON.stringify(state));
    }, SAVE_DEBOUNCE_MS);
    return;
  }
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

/* Returns matrix options for the category modal dropdown.
   Pulls slide-num + h2 text dynamically so labels stay in sync with HTML. */
function getMatrixOptions(){
  return Array.from(document.querySelectorAll('[data-matrix]')).map((m, i)=>{
    const id = m.dataset.matrixId || ('m'+(i+1));
    const slide = m.closest('.slide');
    const num = slide?.querySelector('.slide-num')?.textContent?.trim() || ('שקף '+(i+1));
    const title = slide?.querySelector('h2')?.textContent?.trim() || '';
    return {id, label: title ? `${num} — ${title}` : num};
  });
}

/* ============ HELPERS ============ */
const $ = s=>document.querySelector(s);
const $$ = s=>Array.from(document.querySelectorAll(s));
const uid = ()=>'i_'+Math.random().toString(36).slice(2,10);
function esc(s){return (s||'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))}
function initials(s){if(!s)return '';return s.trim().split(/\s+/).slice(0,2).map(x=>x[0]).join('').toUpperCase()}
function isOverdue(d,s){if(!d||s==='done')return false;return new Date(d)<new Date(new Date().toDateString())}
function fmtDate(d){if(!d)return '';const dt=new Date(d);return dt.toLocaleDateString('he-IL',{day:'2-digit',month:'short'})}
function toast(msg){const t=$('#toast');t.textContent=msg;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(()=>t.classList.remove('show'),1800)}
function matchesQuery(it){
  if(!query) return true;
  const q=query.toLowerCase();
  return (it.title+' '+(it.desc||'')+' '+(it.category||'')+' '+(it.owner||'')).toLowerCase().includes(q);
}
function catIcon(cat){
  // Extract leading emoji if any
  const m = cat.match(/^(\p{Emoji}+)\s*(.*)/u);
  return m ? [m[1], m[2]] : ['', cat];
}

/* ============ RENDER MATRICES ============ */
function renderAllMatrices(){
  const matrices = $$('[data-matrix]');
  matrices.forEach((m, i)=>{
    const matrixId = m.dataset.matrixId || ('m'+(i+1));
    if(!m.dataset.matrixId) m.dataset.matrixId = matrixId;
    const cats = state.categories
      .filter(c=>c.matrixId===matrixId)
      .sort((a,b)=>(a.order||0)-(b.order||0))
      .map(c=>c.label);
    m.innerHTML = renderMatrix(cats, matrixId);
  });
  attachMatrixHandlers();
  updateHeroStats();
  renderTracks();
  renderJourney();
}

/* ============ JOURNEY MAP ============ */
let currentStation = 'start';
let journeyAnimated = false;

const STAGE_MAP = {
  start: ['now'],
  angel: ['angel'],
  vc:    ['vc'],
};
const NEXT_STATION = { start: 'angel', angel: 'vc', vc: null };

const STATION_META = {
  start: {
    title: 'תחילת המסע',
    sub: 'הבסיס שנבנה לפני סבב Angel',
    color: '#10b981',
    iconSvg: '<circle cx="12" cy="12" r="10"/><polygon points="10 8 16 12 10 16 10 8" fill="currentColor"/>',
  },
  angel: {
    title: 'Angel Round',
    sub: 'שלמות מוצר · אבטחה · תשתיות',
    color: '#3b82f6',
    iconSvg: '<path d="M12 2 L14 8 L20 8 L15 12 L17 18 L12 14 L7 18 L9 12 L4 8 L10 8 Z"/>',
  },
  vc: {
    title: 'VC Round',
    sub: 'Scale · גלובלי · אינטליגנציה מתקדמת',
    color: '#8b5cf6',
    iconSvg: '<path d="M2 20 L8 14 L13 19 L22 8"/><polyline points="16 8 22 8 22 14"/>',
  },
};

// Exit criteria per stage — what must be true to move to the next stage
const EXIT_CRITERIA = {
  start: [
    {label:'MVP מוצר חי ב-Production', test:s=>s.now.done >= 4},
    {label:'לפחות 3 תרחישי הונאה מזוהים', test:s=>s.items.filter(i=>i.stage==='now' && i.category.includes('הונאה') && i.status==='done').length >= 3},
    {label:'5+ מוטבים פעילים (pilot)', test:s=>s.now.done >= 5},
    {label:'הצעת ערך ו-pitch מוכנים', test:s=>s.items.filter(i=>i.stage==='now' && i.category.includes('Trust') && i.status==='done').length >= 1},
  ],
  angel: [
    {label:'אבטחת מידע ותקנים (ISO/SOC2 בתהליך)', test:s=>s.items.filter(i=>i.stage==='angel' && i.category.includes('אבטחת') && (i.status==='done' || i.status==='prog')).length >= 2},
    {label:'תשתיות Production ל-scale', test:s=>s.items.filter(i=>i.stage==='angel' && i.category.includes('תשתיות') && i.status==='done').length >= 2},
    {label:'מאות אלפי משתמשים נתמכים', test:s=>s.angel.done >= s.angel.total * 0.7 && s.angel.total > 0},
    {label:'צוות ליבה מגויס (Dev, Compliance)', test:s=>s.items.filter(i=>i.stage==='angel' && i.track==='hiring' && i.status==='done').length >= 1},
  ],
  vc: [
    {label:'מוצר בוגר ומרובה-שפות', test:s=>s.vc.done >= 3},
    {label:'אינטליגנציה מתקדמת (Voice, AI)', test:s=>s.items.filter(i=>i.stage==='vc' && (i.title.includes('קול') || i.title.includes('Voice')) && i.status==='done').length >= 1},
    {label:'הרחבה גלובלית / שפות נוספות', test:s=>s.items.filter(i=>i.stage==='vc' && i.category.includes('שפות') && i.status==='done').length >= 1},
    {label:'Load tests ו-SLA תואמים', test:s=>s.vc.done >= s.vc.total * 0.5 && s.vc.total > 0},
  ],
};

function journeyStats(){
  const s = {items: state.items};
  ['now','angel','vc'].forEach(st=>{
    const items = state.items.filter(i=>i.stage===st);
    const done = items.filter(i=>i.status==='done').length;
    s[st] = {total:items.length, done, prog:items.filter(i=>i.status==='prog').length,
             todo:items.filter(i=>i.status==='todo').length, block:items.filter(i=>i.status==='block').length};
  });
  return s;
}

function renderJourney(){
  const stats = journeyStats();

  // counters + progress + path fills + station done
  [['start','now'], ['angel','angel'], ['vc','vc']].forEach(([st, key])=>{
    const data = stats[key];
    const pct = data.total ? Math.round(data.done/data.total*100) : 0;
    const badge = document.querySelector(`[data-progress="${st}"]`);
    if(badge) badge.textContent = data.total ? `${pct}%` : '0';
    const counter = document.querySelector(`[data-counter="${st}"]`);
    if(counter) counter.textContent = `${data.done}/${data.total}`;
    const stationEl = document.querySelector(`.station[data-stage="${st}"]`);
    if(stationEl) stationEl.classList.toggle('done', data.total>0 && data.done===data.total);

    // fill path segment to pct
    const segIdx = st==='start'?1: st==='angel'?2:3;
    const seg = document.getElementById('pathSeg'+segIdx+'Fill');
    if(seg){
      const L = seg.getTotalLength ? seg.getTotalLength() : 100;
      seg.style.strokeDasharray = L;
      seg.style.strokeDashoffset = L * (1 - pct/100);
    }
  });

  // imbalance warnings
  renderWarnings(stats);
  renderJourneyPanel(currentStation, stats);

  // attach click handlers (once)
  document.querySelectorAll('.station').forEach(st=>{
    if(st._bound) return; st._bound = true;
    st.addEventListener('click', ()=>{
      currentStation = st.dataset.stage;
      document.querySelectorAll('.station').forEach(s=>s.classList.remove('active'));
      st.classList.add('active');
      renderJourney();
      animateTravelDot();
    });
  });

  // First-time page-load animation
  if(!journeyAnimated){
    journeyAnimated = true;
    setTimeout(()=>animateTravelDot(true), 500);
  }
}

function renderWarnings(stats){
  const el = document.getElementById('journeyWarnings');
  if(!el) return;
  const warnings = [];
  // Angel empty but VC has items
  if(stats.angel.total === 0 && stats.vc.total > 0){
    warnings.push({type:'warn', msg:'⚠ תחנת Angel ריקה אך VC כבר מתוכנן — בדקו את הרצף'});
  }
  // all blocked?
  if(stats[currentStation==='start'?'now':currentStation]?.block > 2){
    warnings.push({type:'warn', msg:`⚠ ${stats[currentStation==='start'?'now':currentStation].block} פריטים חסומים — טיפול נדרש`});
  }
  // all todo in current
  const cur = stats[currentStation==='start'?'now':currentStation];
  if(cur && cur.total > 5 && cur.prog === 0 && cur.done === 0){
    warnings.push({type:'info', msg:'💡 טרם התחלתם פריט בתחנה זו — שווה להתחיל משהו קטן'});
  }
  // all done?
  if(cur && cur.total > 0 && cur.done === cur.total){
    warnings.push({type:'success', msg:'🎉 התחנה הושלמה! אפשר להתקדם לתחנה הבאה'});
  }
  el.innerHTML = warnings.map(w=>`<div class="jw-item ${w.type}">${esc(w.msg)}</div>`).join('');
}

function pickCriticalNext(stageItems){
  const prioOrder = {high:0, med:1, low:2};
  const statusOrder = {prog:0, block:1, todo:2, done:3};
  return [...stageItems]
    .filter(i => i.status !== 'done')
    .sort((a,b)=>{
      if(statusOrder[a.status] !== statusOrder[b.status]) return statusOrder[a.status] - statusOrder[b.status];
      return (prioOrder[a.priority]||9) - (prioOrder[b.priority]||9);
    })[0];
}

function renderJourneyPanel(stage, stats){
  if(!stats) stats = journeyStats();
  const meta = STATION_META[stage];
  const stages = STAGE_MAP[stage];
  const items = state.items.filter(i=>stages.includes(i.stage));

  const doneItems = items.filter(i=>i.status==='done');
  const todoItems = items.filter(i=>i.status==='todo' || i.status==='block');
  const nextItems = items.filter(i=>i.status==='prog');

  const total = items.length;
  const pct = total ? Math.round(doneItems.length/total*100) : 0;

  const jpBadge = document.getElementById('jpBadge');
  jpBadge.style.background = meta.color;
  jpBadge.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${meta.iconSvg}</svg>`;
  document.getElementById('jpTitle').textContent = meta.title;
  document.getElementById('jpSub').textContent = meta.sub;

  document.getElementById('jpPct').textContent = pct;
  document.getElementById('jpDoneN').textContent = doneItems.length;
  document.getElementById('jpTotalN').textContent = total;
  const bar = document.getElementById('jpBarFill');
  bar.style.width = pct + '%';
  bar.style.background = meta.color;

  // Critical next step
  const critical = pickCriticalNext(items);
  const critEl = document.getElementById('jpCritical');
  if(critical){
    critEl.className = 'jp-critical';
    critEl.innerHTML = `
      <div class="ico">🎯</div>
      <div class="txt">
        <div class="lbl">הצעד הבא הקריטי</div>
        <div class="ttl">${esc(critical.title)}</div>
      </div>`;
    critEl.onclick = ()=>openModal(critical.id);
  }else{
    critEl.className = 'jp-critical empty';
    critEl.innerHTML = `
      <div class="ico">✓</div>
      <div class="txt">
        <div class="lbl">הצעד הבא הקריטי</div>
        <div class="ttl">כל פריטי התחנה הושלמו — אפשר להתקדם!</div>
      </div>`;
    critEl.onclick = null;
  }

  // Exit criteria
  const exitEl = document.getElementById('jpExit');
  const criteria = EXIT_CRITERIA[stage] || [];
  const metCount = criteria.filter(c=>c.test(stats)).length;
  exitEl.innerHTML = `
    <div class="jp-exit-hdr">
      <span>תנאי יציאה לתחנה הבאה</span>
      <span style="margin-right:auto;color:${metCount===criteria.length?'#047857':'#b45309'}">${metCount}/${criteria.length}</span>
    </div>
    <ul class="jp-exit-list">
      ${criteria.map(c=>{
        const met = c.test(stats);
        return `<li class="${met?'met':'unmet'}"><span class="chk"></span><${met?'span':'strong'}>${esc(c.label)}</${met?'span':'strong'}></li>`;
      }).join('')}
    </ul>`;

  // Lists
  const renderList = (list, elId, countId) => {
    const ul = document.getElementById(elId);
    document.getElementById(countId).textContent = list.length;
    if(list.length === 0){
      ul.innerHTML = `<li class="jp-empty" style="background:transparent;color:#94a3b8;font-style:italic;border:0;justify-content:center">— אין פריטים —</li>`;
      return;
    }
    ul.innerHTML = list.map(it=>{
      const [ico, name] = catIcon(it.category||'');
      return `<li data-jp-item="${it.id}" style="cursor:pointer">
        <strong>${esc(it.title)}</strong>
        <span class="jp-cat">${esc(ico)} ${esc(name).slice(0,14)}${name.length>14?'…':''}</span>
      </li>`;
    }).join('');
    ul.querySelectorAll('[data-jp-item]').forEach(li=>{
      li.addEventListener('click', ()=>openModal(li.dataset.jpItem));
    });
  };

  renderList(doneItems, 'jpDoneList', 'jpDoneC');
  renderList(todoItems, 'jpTodoList', 'jpTodoC');
  renderList(nextItems, 'jpNextList', 'jpNextC');

  // Next station preview
  const nextEl = document.getElementById('jpNextPreview');
  const nxt = NEXT_STATION[stage];
  if(nxt){
    const nxtMeta = STATION_META[nxt];
    const nxtStats = stats[nxt==='start'?'now':nxt];
    nextEl.style.display = 'flex';
    nextEl.innerHTML = `
      <div class="arr" style="background:${nxtMeta.color}">↓</div>
      <div class="txt">
        <div class="lbl">התחנה הבאה</div>
        <div class="nm">${esc(nxtMeta.title)}</div>
        <div class="cnt">${nxtStats.total} פריטים מחכים · לחצו לצפייה</div>
      </div>`;
    nextEl.onclick = ()=>{
      currentStation = nxt;
      document.querySelectorAll('.station').forEach(s=>s.classList.remove('active'));
      document.querySelector(`.station[data-stage="${nxt}"]`)?.classList.add('active');
      renderJourney();
      animateTravelDot();
    };
  }else{
    nextEl.style.display = 'none';
  }

  // Focus btn → scroll to first matrix slide
  const focusBtn = document.getElementById('jpFocusBtn');
  if(focusBtn){
    focusBtn.onclick = ()=>{
      const stageForSearch = {start:'לפני Angel', angel:'Angel', vc:'VC'}[stage];
      query = '';
      const searchEl = document.getElementById('search');
      if(searchEl) searchEl.value = '';
      renderAllMatrices();
      // scroll to first matrix
      const firstMat = document.querySelector('[data-matrix]');
      if(firstMat) firstMat.scrollIntoView({behavior:'smooth', block:'start'});
    };
  }
}

function animateTravelDot(fullJourney){
  const dot = document.getElementById('travelDot');
  if(!dot) return;
  const segs = ['pathSeg1','pathSeg2','pathSeg3'].map(id=>document.getElementById(id));
  const targetIdx = fullJourney ? 2 : ({start:0, angel:1, vc:2}[currentStation]);
  if(targetIdx < 0) return;

  dot.style.opacity = '1';
  let segIdx = 0;

  function animSeg(idx, cb){
    const seg = segs[idx];
    if(!seg){ cb(); return; }
    const L = seg.getTotalLength();
    const duration = 800;
    const start = performance.now();
    (function step(t){
      const progress = Math.min(1, (t - start) / duration);
      const p = seg.getPointAtLength(L * progress);
      dot.setAttribute('cx', p.x);
      dot.setAttribute('cy', p.y);
      const color = ['#10b981','#3b82f6','#8b5cf6'][idx];
      dot.setAttribute('stroke', color);
      if(progress < 1) requestAnimationFrame(step);
      else cb();
    })(performance.now());
  }

  function chain(i){
    if(i > targetIdx){
      setTimeout(()=>{dot.style.opacity = '0'}, 400);
      return;
    }
    animSeg(i, ()=>chain(i+1));
  }
  chain(0);
}

// Export map as PNG (via html-to-image CDN)
async function exportMapAsImage(){
  const map = document.getElementById('journeyMap');
  if(!map){ toast('לא נמצאה מפה'); return; }

  // Load html-to-image on demand
  if(!window.htmlToImage){
    await new Promise((res, rej)=>{
      const s = document.createElement('script');
      s.src = 'https://unpkg.com/html-to-image@1.11.11/dist/html-to-image.js';
      s.onload = res; s.onerror = rej;
      document.head.appendChild(s);
    }).catch(()=>toast('שגיאה בטעינת מודול הייצוא'));
  }
  if(!window.htmlToImage){ toast('הייצוא לא זמין'); return; }

  toast('מייצר תמונה…');
  try{
    const dataUrl = await window.htmlToImage.toPng(map, {pixelRatio:2, backgroundColor:'#f8fafc'});
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = 'ASPS-Journey-Map.png';
    a.click();
    toast('התמונה ירדה ✓');
  }catch(e){
    console.error(e);
    toast('שגיאה בייצוא');
  }
}
document.addEventListener('click', e=>{
  if(e.target.closest('#btnExportMap')) exportMapAsImage();
});

function renderMatrix(cats, matrixId){
  // header counts per stage within this matrix's categories
  const itemsInMatrix = state.items.filter(i=>cats.includes(i.category));
  const counts = {now:0,angel:0,vc:0};
  itemsInMatrix.forEach(i=>{if(counts[i.stage]!==undefined) counts[i.stage]++});

  let html = `<div class="matrix-header">
    <div></div>
    <div class="col-title now-col">לפני Angel <span class="col-count">${counts.now}</span></div>
    <div class="col-title angel-col">בשלב Angel <span class="col-count">${counts.angel}</span></div>
    <div class="col-title vc-col">בשלב VC <span class="col-count">${counts.vc}</span></div>
  </div>`;

  cats.forEach(cat=>{
    const [ico, name] = catIcon(cat);
    // Sort items in this category by `order` so drag-reorder is respected.
    const catItems = state.items
      .filter(i=>i.category===cat)
      .sort((a,b)=>(a.order||0)-(b.order||0));
    const totalVisible = catItems.filter(matchesQuery).length;
    const hiddenCat = query && totalVisible===0 ? 'hidden' : '';

    const catEntry = state.categories.find(c=>c.label===cat);
    const isCollapsed = !!(catEntry && catEntry.collapsed);
    const collapsedCls = isCollapsed ? 'collapsed' : '';
    html += `<div class="category-block ${hiddenCat} ${collapsedCls}" data-cat-block="${esc(cat)}">
      <div class="category-title">
        <button class="cat-toggle" data-toggle-cat="${esc(cat)}" title="כווץ / פתח קטגוריה" aria-label="toggle">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>
        </button>
        <span class="cat-icon">${esc(ico)}</span>
        <span>${esc(name)}</span>
        <span class="cat-count">${catItems.length} פריטים</span>
        <button class="cat-edit" data-edit-cat="${esc(cat)}" title="ערוך קטגוריה">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 1 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>
        </button>
        <button class="cat-add" data-add-cat="${esc(cat)}" title="הוסף פריט בקטגוריה זו">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        </button>
      </div>`;

    // Render rows. Layout strategy: one row per stage group with at least 1 item (for that cat).
    // To preserve "row per task" look where natural, we render each item in its own mini row in its stage column,
    // with empty cells in the other columns. BUT items assigned to "gap" show in Now column (since gap doesn't have a column here).
    // Simplest + good UX: one row per item, showing item in its stage column, empty-add cells in others.
    if(catItems.length===0){
      html += `<div class="matrix-row">
        <div class="row-label" style="color:#cbd5e1;font-style:italic">אין פריטים — הוסיפו אחד</div>
        <div class="cell empty" data-add-cell="${esc(cat)}|now"><span class="add-hint"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg> הוסף</span></div>
        <div class="cell empty" data-add-cell="${esc(cat)}|angel"><span class="add-hint"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg> הוסף</span></div>
        <div class="cell empty" data-add-cell="${esc(cat)}|vc"><span class="add-hint"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg> הוסף</span></div>
      </div>`;
    }else{
      catItems.forEach(it=>{
        const hidden = !matchesQuery(it) ? 'hidden' : '';
        const displayStage = it.stage==='gap' ? 'now' : it.stage; // show gap items in Now column visually, but styled as gap
        html += `<div class="matrix-row ${hidden}" data-item-row="${it.id}">
          <div class="row-label">
            <span class="rl-text">${esc(it.title)}</span>
            ${it.stage==='gap' ? '<span class="rl-gap">❓ לא מסווג</span>' : ''}
          </div>
          ${['now','angel','vc'].map(stg=>{
            if(displayStage===stg){
              return `<div class="cell ${it.stage==='gap' ? 'gap-cell' : STAGES[it.stage].cellCls}" data-cell-stage="${stg}" data-item-cell="${it.id}">
                ${renderMiniItem(it)}
              </div>`;
            }else{
              return `<div class="cell empty" data-move-cell="${it.id}|${stg}" data-add-cell="${esc(cat)}|${stg}">
                <span class="add-hint"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg> הוסף / גרור לכאן</span>
              </div>`;
            }
          }).join('')}
        </div>`;
      });
    }

    html += `</div>`;
  });

  if(matrixId){
    html += `<div class="add-category-row">
      <button class="add-cat-btn" data-add-cat-to="${esc(matrixId)}" type="button">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        הוסף קטגוריה לשקף זה
      </button>
    </div>`;
  }

  return html;
}

function renderMiniItem(it){
  const overdue = isOverdue(it.due, it.status);
  const prioLabel = it.priority==='high'?'גבוהה':it.priority==='med'?'בינונית':'נמוכה';
  return `<div class="mini-item" draggable="true" data-item="${it.id}">
    <span class="status-dot ${it.status}" data-status-toggle="${it.id}" title="${STATUSES[it.status]} — לחץ לשינוי"></span>
    <span class="m-title">${esc(it.title)}</span>
  </div>
  <div class="mini-meta">
    ${it.jira ? `<span class="mm jira" title="JIRA: ${esc(it.jira)}">${esc(it.jira)}</span>` : ''}
    ${it.priority ? `<span class="mm prio-${it.priority}">${prioLabel}</span>` : ''}
    ${it.effort ? `<span class="mm">${it.effort}</span>` : ''}
    ${it.due ? `<span class="mm ${overdue?'overdue':''}">📅 ${fmtDate(it.due)}</span>` : ''}
    ${it.owner ? `<span class="owner-av" title="${esc(it.owner)}">${initials(it.owner)}</span>` : ''}
  </div>`;
}

// Track which kind of element is currently being dragged so handlers can filter.
// 'item' | 'cat' | null
let _dragKind = null;

function attachMatrixHandlers(){
  // click mini-item => edit
  $$('.mini-item').forEach(el=>{
    el.addEventListener('click', e=>{
      if(e.target.closest('[data-status-toggle]')) return;
      openModal(el.dataset.item);
    });
    el.addEventListener('dragstart', e=>{
      _dragKind = 'item';
      el.classList.add('dragging');
      e.dataTransfer.setData('text/plain', el.dataset.item);
      e.dataTransfer.effectAllowed='move';
    });
    el.addEventListener('dragend', ()=>{
      _dragKind = null;
      el.classList.remove('dragging');
    });
  });

  // status dot click -> cycle status
  $$('[data-status-toggle]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      const id = el.dataset.statusToggle;
      const it = state.items.find(i=>i.id===id);
      if(!it) return;
      const order = ['todo','prog','done','block'];
      const idx = order.indexOf(it.status);
      it.status = order[(idx+1) % order.length];
      it.updatedAt = Date.now();
      save();
      renderAllMatrices();
      toast('סטטוס: ' + STATUSES[it.status]);
    });
  });

  // click add on empty cell => add new in that category+stage
  $$('[data-add-cell]').forEach(el=>{
    el.addEventListener('click', e=>{
      // don't open if user dropped here
      if(el.dataset.moveCell && el._justDropped) { el._justDropped=false; return; }
      const [cat, stage] = el.dataset.addCell.split('|');
      openModal(null, {category:cat, stage:stage});
    });
    el.addEventListener('dragover', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return; // category drag bubbles to category-block
      e.preventDefault();el.classList.add('drag-over');
    });
    el.addEventListener('dragleave', ()=>el.classList.remove('drag-over'));
    el.addEventListener('drop', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return;
      e.preventDefault();el.classList.remove('drag-over');
      const id = e.dataTransfer.getData('text/plain');
      const [cat, stage] = (el.dataset.addCell||'').split('|');
      const dragged = state.items.find(i=>i.id===id);
      if(!dragged) return;
      // Move to end of target category
      const targetCount = state.items.filter(i=>i.category===cat && i.id!==id).length;
      const oldCat = dragged.category;
      moveItemToPosition(id, cat, stage, targetCount);
      el._justDropped = true;
      renderAllMatrices();
      toast(oldCat===cat ? 'הועבר ל' + STAGES[stage].label : 'הועבר לקטגוריה: ' + cat);
    });
  });

  // Occupied cells: drop reorders within target category (above/below by Y position)
  $$('[data-item-cell]').forEach(el=>{
    el.addEventListener('dragover', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return; // category drag bubbles up
      e.preventDefault();
      el.classList.add('drag-over');
      const row = el.closest('.matrix-row');
      if(row){
        const rect = row.getBoundingClientRect();
        const above = e.clientY < rect.top + rect.height/2;
        row.classList.toggle('drop-above', above);
        row.classList.toggle('drop-below', !above);
      }
    });
    el.addEventListener('dragleave', ()=>{
      el.classList.remove('drag-over');
      const row = el.closest('.matrix-row');
      if(row) row.classList.remove('drop-above','drop-below');
    });
    el.addEventListener('drop', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return;
      e.preventDefault();
      el.classList.remove('drag-over');
      const row = el.closest('.matrix-row');
      if(row) row.classList.remove('drop-above','drop-below');

      const id = e.dataTransfer.getData('text/plain');
      const targetItemId = el.dataset.itemCell;
      const targetStage = el.dataset.cellStage;
      const dragged = state.items.find(i=>i.id===id);
      const target = state.items.find(i=>i.id===targetItemId);
      if(!dragged || !target || dragged.id===target.id) return;

      // Compute insert position relative to target inside target's category
      const catItems = state.items
        .filter(i=>i.category===target.category && i.id!==dragged.id)
        .sort((a,b)=>(a.order||0)-(b.order||0));
      const targetIdx = catItems.findIndex(i=>i.id===target.id);
      const rect = (row||el).getBoundingClientRect();
      const insertAbove = e.clientY < rect.top + rect.height/2;
      const pos = (insertAbove ? targetIdx : targetIdx+1);

      const sameCat = dragged.category === target.category;
      const sameStage = dragged.stage === targetStage;
      moveItemToPosition(id, target.category, targetStage, pos);
      renderAllMatrices();
      toast(sameCat
        ? (sameStage ? 'סודר מחדש' : 'הועבר ל' + STAGES[targetStage].label)
        : 'הועבר לקטגוריה: ' + target.category);
    });
  });

  // Category title drag-and-drop:
  //   * For ITEM drag: dropping on title moves item to end of that category
  //   * For CATEGORY drag: kick off category drag from the title
  $$('[data-cat-block]').forEach(block=>{
    const title = block.querySelector('.category-title');
    if(!title) return;

    // Make the title a drag handle for the category
    title.draggable = true;
    title.addEventListener('dragstart', e=>{
      // Don't start drag if user grabbed an inner button/input
      if(e.target.closest('button, input')) { e.preventDefault(); return; }
      _dragKind = 'cat';
      block.classList.add('dragging-cat');
      e.dataTransfer.setData('text/plain', block.dataset.catBlock);
      e.dataTransfer.effectAllowed='move';
    });
    title.addEventListener('dragend', ()=>{
      _dragKind = null;
      block.classList.remove('dragging-cat');
    });

    // Item-drag drop on title => move item to end of this category
    title.addEventListener('dragover', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return; // category drop is handled at block level
      e.preventDefault(); title.classList.add('drag-over');
    });
    title.addEventListener('dragleave', ()=>title.classList.remove('drag-over'));
    title.addEventListener('drop', e=>{
      if(_dragKind==='cat' || _dragKind==='slide') return;
      e.preventDefault(); title.classList.remove('drag-over');
      const id = e.dataTransfer.getData('text/plain');
      const cat = block.dataset.catBlock;
      const dragged = state.items.find(i=>i.id===id);
      if(!dragged) return;
      const targetCount = state.items.filter(i=>i.category===cat && i.id!==id).length;
      moveItemToPosition(id, cat, dragged.stage==='gap' ? 'now' : dragged.stage, targetCount);
      renderAllMatrices();
      toast('הועבר לקטגוריה: ' + cat);
    });

    // Category-drag drop on this block => reorder/move category
    block.addEventListener('dragover', e=>{
      if(_dragKind!=='cat') return;
      if(block.classList.contains('dragging-cat')) return; // can't drop on self
      e.preventDefault();
      const rect = block.getBoundingClientRect();
      const above = e.clientY < rect.top + rect.height/2;
      block.classList.toggle('cat-drop-above', above);
      block.classList.toggle('cat-drop-below', !above);
    });
    block.addEventListener('dragleave', ()=>{
      block.classList.remove('cat-drop-above','cat-drop-below');
    });
    block.addEventListener('drop', e=>{
      if(_dragKind!=='cat') return;
      if(block.classList.contains('dragging-cat')) return;
      e.preventDefault();
      block.classList.remove('cat-drop-above','cat-drop-below');
      const draggedLabel = e.dataTransfer.getData('text/plain');
      const targetLabel = block.dataset.catBlock;
      if(!draggedLabel || draggedLabel===targetLabel) return;
      const targetMatrix = block.closest('[data-matrix]');
      const targetMatrixId = targetMatrix?.dataset.matrixId;
      if(!targetMatrixId) return;
      const rect = block.getBoundingClientRect();
      const above = e.clientY < rect.top + rect.height/2;
      moveCategoryToMatrix(draggedLabel, targetMatrixId, targetLabel, above);
      renderAllMatrices();
      const dragCat = state.categories.find(c=>c.label===draggedLabel);
      toast(dragCat ? 'הקטגוריה הועברה' : 'נכשל');
    });
  });

  // Matrix-level drop for categories (drop in empty area => end of matrix)
  $$('[data-matrix]').forEach(mat=>{
    mat.addEventListener('dragover', e=>{
      if(_dragKind!=='cat') return;
      // If a child block already handled this, let it. Otherwise accept end-drop.
      if(e.target.closest('[data-cat-block]')) return;
      e.preventDefault();
      mat.classList.add('cat-drop-end');
    });
    mat.addEventListener('dragleave', e=>{
      // Only remove if leaving the matrix entirely
      if(!mat.contains(e.relatedTarget)) mat.classList.remove('cat-drop-end');
    });
    mat.addEventListener('drop', e=>{
      mat.classList.remove('cat-drop-end');
      if(_dragKind!=='cat') return;
      if(e.target.closest('[data-cat-block]')) return; // child handled
      e.preventDefault();
      const draggedLabel = e.dataTransfer.getData('text/plain');
      const targetMatrixId = mat.dataset.matrixId;
      if(!draggedLabel || !targetMatrixId) return;
      moveCategoryToMatrix(draggedLabel, targetMatrixId, null, false);
      renderAllMatrices();
      toast('הקטגוריה הועברה לסוף השקף');
    });
  });

  // category + button (adds an ITEM in that category)
  $$('[data-add-cat]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      openModal(null, {category: el.dataset.addCat, stage:'now'});
    });
  });

  // "+ Add category to this slide" button at the bottom of each matrix
  $$('[data-add-cat-to]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      openCatModal(el.dataset.addCatTo);
    });
  });

  // Edit category (pencil icon in category title)
  $$('[data-edit-cat]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      openCatModal(null, el.dataset.editCat);
    });
  });

  // Collapse / expand a category (chevron at start of title)
  $$('[data-toggle-cat]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.stopPropagation();
      const label = el.dataset.toggleCat;
      const cat = state.categories.find(c=>c.label===label);
      if(!cat) return;
      cat.collapsed = !cat.collapsed;
      save();
      // Toggle CSS class only — keeps animation smooth, avoids re-render flicker
      const block = el.closest('.category-block');
      if(block) block.classList.toggle('collapsed', cat.collapsed);
    });
  });
}

/* ============ HERO STATS ============ */
function updateHeroStats(){
  const el = $('#heroStats');
  if(!el) return;
  const total = state.items.length;
  const done = state.items.filter(i=>i.status==='done').length;
  const gap = state.items.filter(i=>i.stage==='gap').length;
  const pct = total? Math.round(done/total*100) : 0;
  el.innerHTML = `
    <div class="stat"><span class="n">${total}</span><span class="l">פריטים</span></div>
    <div class="stat"><span class="n">${done}</span><span class="l">הושלמו</span></div>
    <div class="stat"><span class="n">${pct}%</span><span class="l">התקדמות</span></div>
    <div class="stat"><span class="n" style="color:${gap>0?'#fcd34d':'#10b981'}">${gap}</span><span class="l">לא מסווג</span></div>
  `;
}

/* ============ TRACKS ============ */
function renderTracks(){
  const grid = $('#tracksGrid');
  if(!grid) return;
  grid.innerHTML = Object.entries(TRACKS).map(([k,t])=>{
    const items = state.items.filter(i=>i.track===k);
    const done = items.filter(i=>i.status==='done').length;
    const prog = items.filter(i=>i.status==='prog').length;
    const block = items.filter(i=>i.status==='block').length;
    const total = items.length;
    // Build progress segs
    let segs='';
    for(let i=0;i<10;i++){
      const threshold = (i+1)/10;
      const doneRatio = total ? done/total : 0;
      const progRatio = total ? (done+prog)/total : 0;
      const blockRatio = total ? (done+prog+block)/total : 0;
      let cls='';
      if(threshold<=doneRatio) cls='done';
      else if(threshold<=progRatio) cls='prog';
      else if(threshold<=blockRatio) cls='block';
      segs += `<div class="seg ${cls}"></div>`;
    }
    // Top 4 items
    const top = items.slice(0,5);
    return `<div class="track ${t.cls}">
      <h3>${t.ico} ${t.label}</h3>
      <div class="t-sub">${t.sub}</div>
      <div class="track-progress">${segs}</div>
      <ul>
        ${top.map(it=>`<li><span class="status-dot ${it.status}"></span>${esc(it.title)}</li>`).join('')}
        ${items.length>5?`<li style="color:var(--gray);font-style:italic">+ ${items.length-5} נוספים</li>`:''}
        ${items.length===0?`<li style="color:var(--gray);font-style:italic">אין פריטים</li>`:''}
      </ul>
      <div class="t-count" style="margin-top:12px">
        <span>סה״כ: <strong>${total}</strong></span>
        <span>· הושלמו: <strong style="color:var(--now)">${done}</strong></span>
        <span>· בביצוע: <strong style="color:var(--angel)">${prog}</strong></span>
        ${block>0?`<span>· חסום: <strong style="color:#ef4444">${block}</strong></span>`:''}
      </div>
    </div>`;
  }).join('');
}

/* ============ MODAL ============ */
function openModal(id, presets={}){
  editingId = id;
  const isEdit = !!id;
  $('#modalTitle').textContent = isEdit ? 'עריכת פריט' : 'פריט חדש';
  $('#modalDelete').style.display = isEdit ? 'inline-flex' : 'none';

  let it = isEdit ? {...state.items.find(i=>i.id===id)} : {
    title:'', desc:'', stage:'now', status:'todo', priority:'med',
    track:'product', effort:'', due:'', owner:'', category:'', jira:''
  };
  if(!isEdit) Object.assign(it, presets);
  modalDraft = it;

  $('#fTitle').value = it.title||'';
  $('#fDesc').value = it.desc||'';
  $('#fTrackSel').value = it.track||'product';
  $('#fEffortSel').value = it.effort||'';
  $('#fDue').value = it.due||'';
  $('#fOwner').value = it.owner||'';
  $('#fJira').value = it.jira||'';

  setSeg('stage', it.stage);
  setSeg('status', it.status);
  setSeg('priority', it.priority);

  // Populate category select from state.categories (grouped by matrix slide)
  const itemCat = it.category||'';
  const noCat = !itemCat || itemCat === 'ללא קטגוריה';
  const matrixOpts = getMatrixOptions();
  let catHtml = '<option value="">— ללא קטגוריה —</option>';
  // Orphan: item has a category that no longer exists in state.categories
  if(itemCat && !noCat && !state.categories.some(c=>c.label===itemCat)){
    catHtml += `<option value="${esc(itemCat)}">⚠ ${esc(itemCat)} (לא משויכת לשקף)</option>`;
  }
  matrixOpts.forEach(mo=>{
    const inMatrix = state.categories
      .filter(c=>c.matrixId===mo.id)
      .sort((a,b)=>(a.order||0)-(b.order||0));
    if(!inMatrix.length) return;
    catHtml += `<optgroup label="${esc(mo.label)}">`;
    inMatrix.forEach(c=>{
      catHtml += `<option value="${esc(c.label)}">${esc(c.label)}</option>`;
    });
    catHtml += `</optgroup>`;
  });
  $('#fCategory').innerHTML = catHtml;
  $('#fCategory').value = noCat ? '' : itemCat;

  $('#modalBg').classList.add('open');
  setTimeout(()=>$('#fTitle').focus(),60);
}
function setSeg(key, val){
  $$(`[data-seg="${key}"]`).forEach(b=>{
    b.classList.toggle('active', b.dataset.val===val);
  });
  modalDraft[key] = val;
}
function closeModal(){$('#modalBg').classList.remove('open');editingId=null}
function saveModal(){
  const title = $('#fTitle').value.trim();
  if(!title){$('#fTitle').focus();toast('כותרת חובה');return}
  const category = $('#fCategory').value.trim() || 'ללא קטגוריה';
  // Auto-create the category if it's a new one (so the item actually shows up in a matrix).
  // Default placement: first matrix on the page.
  if(category && category !== 'ללא קטגוריה' && !state.categories.some(c=>c.label===category)){
    const firstMatrix = $$('[data-matrix]')[0];
    const matrixId = firstMatrix?.dataset.matrixId || 'm1';
    const order = state.categories
      .filter(c=>c.matrixId===matrixId)
      .reduce((m,c)=>Math.max(m, c.order||0), -1) + 1;
    state.categories.push({label:category, matrixId, order});
    toast('קטגוריה חדשה נוצרה: ' + category);
  }
  const payload = {
    title,
    desc: $('#fDesc').value.trim(),
    stage: modalDraft.stage||'now',
    status: modalDraft.status||'todo',
    priority: modalDraft.priority||'med',
    track: $('#fTrackSel').value,
    effort: $('#fEffortSel').value,
    due: $('#fDue').value,
    owner: $('#fOwner').value.trim(),
    jira: $('#fJira').value.trim(),
    category,
    updatedAt: Date.now(),
  };
  if(editingId){
    Object.assign(state.items.find(i=>i.id===editingId), payload);
    toast('נשמר');
  }else{
    const maxOrder = state.items.reduce((m,i)=>Math.max(m,i.order||0),0);
    state.items.push({id:uid(), order:maxOrder+1, ...payload});
    toast('פריט חדש נוצר');
  }
  save(); closeModal(); renderAllMatrices();
}
function deleteItem(){
  if(!editingId) return;
  if(!confirm('למחוק את הפריט?')) return;
  state.items = state.items.filter(i=>i.id!==editingId);
  save(); closeModal(); renderAllMatrices();
  toast('נמחק');
}

/* ============ CATEGORY MODAL ============ */
let catEditingLabel = null;

function openCatModal(presetMatrixId, editingLabel){
  catEditingLabel = editingLabel || null;
  const isEdit = !!catEditingLabel;

  let existing = null;
  if(isEdit){
    existing = state.categories.find(c=>c.label===catEditingLabel);
    if(!existing){toast('קטגוריה לא נמצאה'); catEditingLabel=null; return}
  }

  // Pre-fill icon + name
  if(existing){
    const [ico, name] = catIcon(existing.label);
    $('#fCatIcon').value = ico;
    $('#fCatName').value = name;
  }else{
    $('#fCatIcon').value = '';
    $('#fCatName').value = '';
  }

  // Build matrix dropdown
  const opts = getMatrixOptions();
  $('#fCatMatrix').innerHTML = opts.map(o=>
    `<option value="${esc(o.id)}">${esc(o.label)}</option>`
  ).join('');
  $('#fCatMatrix').value = (existing && existing.matrixId) || presetMatrixId || (opts[0]?.id || '');
  // Lock matrix selector only when adding from inside a specific matrix (not for edit)
  $('#fCatMatrix').disabled = !isEdit && !!presetMatrixId;

  // Update modal labels + buttons
  $('#modalCatTitle').textContent = isEdit ? 'עריכת קטגוריה' : 'קטגוריה חדשה';
  $('#modalCatSave').textContent = isEdit ? 'שמירה' : 'הוסף';
  $('#modalCatDelete').style.display = isEdit ? 'inline-flex' : 'none';

  $('#modalCatBg').classList.add('open');
  setTimeout(()=>$('#fCatName').focus(),60);
}
function closeCatModal(){
  $('#modalCatBg').classList.remove('open');
  $('#fCatMatrix').disabled = false;
  catEditingLabel = null;
  $('#modalCatTitle').textContent = 'קטגוריה חדשה';
  $('#modalCatSave').textContent = 'הוסף';
  $('#modalCatDelete').style.display = 'none';
}
function buildCatLabel(icon, name){
  const i = (icon||'').trim(); const n = (name||'').trim();
  return i ? `${i} ${n}` : n;
}
function addCategory({icon, name, matrixId}){
  const label = buildCatLabel(icon, name);
  if(!label){toast('יש להזין שם'); return null}
  if(state.categories.some(c=>c.label===label)){
    toast('קטגוריה זו כבר קיימת'); return null;
  }
  const order = state.categories
    .filter(c=>c.matrixId===matrixId)
    .reduce((m,c)=>Math.max(m, c.order||0), -1) + 1;
  const entry = {label, matrixId, order};
  state.categories.push(entry);
  save();
  return entry;
}
function updateCategory(oldLabel, {icon, name, matrixId}){
  const newLabel = buildCatLabel(icon, name);
  if(!newLabel) return {ok:false, err:'יש להזין שם'};
  const cat = state.categories.find(c=>c.label===oldLabel);
  if(!cat) return {ok:false, err:'קטגוריה לא נמצאה'};
  // Conflict only if a DIFFERENT category already has the new label
  if(newLabel !== oldLabel && state.categories.some(c=>c.label===newLabel)){
    return {ok:false, err:'קטגוריה עם שם זה כבר קיימת'};
  }
  const oldMatrix = cat.matrixId;
  // Cascade label change to all items
  if(newLabel !== oldLabel){
    state.items.forEach(it=>{ if(it.category===oldLabel) it.category = newLabel });
    cat.label = newLabel;
  }
  // If matrix changed, append to end of new matrix
  if(matrixId !== oldMatrix){
    const order = state.categories
      .filter(c=>c.matrixId===matrixId && c!==cat)
      .reduce((m,c)=>Math.max(m, c.order||0), -1) + 1;
    cat.matrixId = matrixId;
    cat.order = order;
  }
  save();
  return {ok:true, cat};
}
function deleteCat(){
  if(!catEditingLabel) return;
  const cat = state.categories.find(c=>c.label===catEditingLabel);
  if(!cat) return;
  const itemsInCat = state.items.filter(i=>i.category===catEditingLabel);
  let msg = `למחוק את הקטגוריה "${catEditingLabel}"?`;
  if(itemsInCat.length>0){
    msg += `\n\n⚠ ${itemsInCat.length} פריטים בקטגוריה זו יישארו במערכת אך לא יוצגו בשום שקף עד שתעבירו אותם לקטגוריה אחרת.`;
  }
  if(!confirm(msg)) return;
  state.categories = state.categories.filter(c=>c.label!==catEditingLabel);
  save();
  const removed = catEditingLabel;
  closeCatModal();
  renderAllMatrices();
  toast(`נמחקה: ${removed}`);
}
function saveCat(){
  const icon = $('#fCatIcon').value.trim();
  const name = $('#fCatName').value.trim();
  const matrixId = $('#fCatMatrix').value;
  if(!name){$('#fCatName').focus(); toast('יש להזין שם'); return}
  if(!matrixId){toast('יש לבחור שקף'); return}

  if(catEditingLabel){
    // EDIT mode
    const res = updateCategory(catEditingLabel, {icon, name, matrixId});
    if(!res.ok){toast(res.err); return}
    closeCatModal();
    renderAllMatrices();
    toast('קטגוריה עודכנה: ' + res.cat.label);
    return;
  }

  // ADD mode
  const entry = addCategory({icon, name, matrixId});
  if(!entry) return;
  closeCatModal();
  renderAllMatrices();
  toast('קטגוריה נוספה: ' + entry.label);
  // Scroll the new category (last block in the matrix) into view
  const targetMatrix = document.querySelector(`[data-matrix-id="${matrixId}"]`);
  if(targetMatrix){
    const blocks = targetMatrix.querySelectorAll('[data-cat-block]');
    const lastBlock = blocks[blocks.length - 1];
    (lastBlock || targetMatrix).scrollIntoView({behavior:'smooth', block:'center'});
  }
}

/* ============ REMOTE SYNC (GitHub-hosted JSON) ============ */
// Loaded relative to the HTML file. Override by setting `data-remote-url` on <body>.
const REMOTE_URL = (document.body.dataset.remoteUrl || 'asps-roadmap.json');

/* Apply a remote payload to local state. Preserves slides/categories migration if missing. */
function applyRemoteData(p){
  state = {
    items: p.items,
    categories: Array.isArray(p.categories) && p.categories.length ? p.categories : deriveCategoriesFromHTML(),
    slides:     Array.isArray(p.slides)     && p.slides.length     ? p.slides     : deriveSlidesFromHTML(),
    exportedAt: p.exportedAt || null,
  };
  save();
}

/* Returns one of: 'cold' | 'replaced' | 'kept-local' | 'same' | 'error' | 'no-remote' */
async function trySyncFromRemote(opts={}){
  const interactive = !!opts.interactive;
  let res;
  try{
    res = await fetch(REMOTE_URL, {cache:'no-store'});
  }catch(e){
    if(interactive) toast('סנכרון נכשל: בדוק שהקובץ קיים והדף ב-HTTPS/GitHub Pages');
    return 'error';
  }
  if(!res.ok){
    if(interactive) toast(`לא נמצא קובץ ${REMOTE_URL} (HTTP ${res.status})`);
    return 'no-remote';
  }
  let data;
  try{ data = await res.json(); }
  catch(e){ if(interactive) toast('הקובץ ב-GitHub לא JSON תקין'); return 'error'; }
  if(!data.items || !Array.isArray(data.items)){
    if(interactive) toast('פורמט הקובץ לא תקין (חסר items)');
    return 'error';
  }

  const localExportedAt = state.exportedAt || null;
  const remoteExportedAt = data.exportedAt || null;
  if(localExportedAt && remoteExportedAt && localExportedAt === remoteExportedAt){
    if(interactive) toast('כבר מסונכרן עם GitHub');
    return 'same';
  }

  // First-ever load: nothing in localStorage besides the seed-derived defaults
  const hasSavedLocal = !!localStorage.getItem(STORAGE_KEY);
  if(!hasSavedLocal && !interactive){
    applyRemoteData(data);
    return 'cold';
  }

  // Has local data and remote differs - confirm
  const localStr  = localExportedAt  ? new Date(localExportedAt ).toLocaleString('he-IL') : '— (לא ידוע)';
  const remoteStr = remoteExportedAt ? new Date(remoteExportedAt).toLocaleString('he-IL') : '— (לא ידוע)';
  const ok = confirm(
    `📥 קיימת גרסה אחרת ב-GitHub\n\n` +
    `מקומי:  ${localStr}\n` +
    `GitHub: ${remoteStr}\n\n` +
    `לטעון מ-GitHub? השינויים המקומיים שלא יוצאו יידרסו.`
  );
  if(!ok) return 'kept-local';
  applyRemoteData(data);
  return 'replaced';
}

/* ============ EXPORT / IMPORT ============ */
function exportJson(){
  const exportedAt = new Date().toISOString();
  state.exportedAt = exportedAt;  // remember this version so we can detect remote diffs later
  save();
  const data = JSON.stringify({
    version:4,
    exportedAt,
    items:state.items,
    categories:state.categories,
    slides:state.slides,
  }, null, 2);
  const blob = new Blob([data],{type:'application/json'});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  // Canonical filename for GitHub sharing (replace previous on commit)
  a.href = url; a.download = 'asps-roadmap.json';
  a.click(); URL.revokeObjectURL(url);
  toast('יוצא — העלה ל-GitHub כדי לשתף');
}
function importJson(e){
  const f = e.target.files[0]; if(!f) return;
  const r = new FileReader();
  r.onload = ev=>{
    try{
      const p = JSON.parse(ev.target.result);
      if(!p.items || !Array.isArray(p.items)) throw new Error('פורמט לא תקין');
      const catCount = Array.isArray(p.categories) ? p.categories.length : 0;
      const slideCount = Array.isArray(p.slides) ? p.slides.length : 0;
      const summary = [`${p.items.length} פריטים`, catCount?`${catCount} קטגוריות`:'', slideCount?`${slideCount} שקפים`:''].filter(Boolean).join(', ');
      if(!confirm(`לייבא ${summary}? זה יחליף את הנתונים הנוכחיים.`)) return;
      state = {
        items: p.items,
        categories: Array.isArray(p.categories) && p.categories.length ? p.categories : deriveCategoriesFromHTML(),
        slides:     Array.isArray(p.slides)     && p.slides.length     ? p.slides     : deriveSlidesFromHTML(),
      };
      save(); renderAllSlides();
      toast(`יובאו ${p.items.length} פריטים`);
    }catch(err){alert('קובץ לא תקין: '+err.message)}
  };
  r.readAsText(f); e.target.value='';
}

/* ============ WIRE ============ */
async function syncAndRender(){
  if (ADMIN_MODE) { renderAllSlides(); return; }
  // Try a silent sync from GitHub before initial render. Don't block on errors.
  try{
    const r = await trySyncFromRemote({interactive:false});
    if(r === 'cold')     toast('נתונים נטענו מ-GitHub');
    if(r === 'replaced') toast('סונכרן מ-GitHub');
  }catch(_){/* ignore */}
  renderAllSlides();
}

function init(){
  if (ADMIN_MODE) {
    // Server posts a flushed state when the user clicks the manual "save" button.
    window.RoadmapAdmin.getCurrentData = () => {
      // Flush any pending debounced save first so we don't lose latest edits.
      if (_savePending) { clearTimeout(_saveTimer); _savePending = false; }
      return JSON.stringify(state);
    };
    // Keep the URL behaviour clean: skip "Reset to defaults" callback and seed-from-HTML pieces
    // (those remain available for the standalone docs HTML).
  }
  // Wire up handlers first (these target static DOM that exists before any data render)
  $('#search').addEventListener('input', e=>{query=e.target.value;renderAllMatrices()});

  $('#btnAdd').addEventListener('click', ()=>openModal(null));
  $('#btnExport').addEventListener('click', exportJson);
  $('#btnImport').addEventListener('click', ()=>$('#fileImport').click());
  $('#fileImport').addEventListener('change', importJson);
  $('#btnPrint').addEventListener('click', ()=>window.print());
  $('#btnSync').addEventListener('click', async ()=>{
    const r = await trySyncFromRemote({interactive:true});
    if(r === 'replaced'){ renderAllSlides(); toast('סונכרן מ-GitHub'); }
    else if(r === 'cold'){ renderAllSlides(); toast('נטען מ-GitHub'); }
  });
  $('#btnReset').addEventListener('click', ()=>{
    if(confirm('לאפס את כל הנתונים לברירת המחדל? לא ניתן לבטל.')){
      state=seedState();save();renderAllSlides();toast('אופס לברירת מחדל');
    }
  });

  // item modal
  $('#modalClose').addEventListener('click', closeModal);
  $('#modalCancel').addEventListener('click', closeModal);
  $('#modalBg').addEventListener('click', e=>{if(e.target.id==='modalBg')closeModal()});
  $('#modalSave').addEventListener('click', saveModal);
  $('#modalDelete').addEventListener('click', deleteItem);

  // category modal
  $('#btnAddCat').addEventListener('click', ()=>openCatModal(null));
  $('#modalCatClose').addEventListener('click', closeCatModal);
  $('#modalCatCancel').addEventListener('click', closeCatModal);
  $('#modalCatBg').addEventListener('click', e=>{if(e.target.id==='modalCatBg')closeCatModal()});
  $('#modalCatSave').addEventListener('click', saveCat);
  $('#modalCatDelete').addEventListener('click', deleteCat);

  // slide modal
  $('#btnAddSlide').addEventListener('click', ()=>openSlideModal(null));
  $('#modalSlideClose').addEventListener('click', closeSlideModal);
  $('#modalSlideCancel').addEventListener('click', closeSlideModal);
  $('#modalSlideBg').addEventListener('click', e=>{if(e.target.id==='modalSlideBg')closeSlideModal()});
  $('#modalSlideSave').addEventListener('click', saveSlideModal);
  $('#modalSlideDelete').addEventListener('click', ()=>{
    if(slideEditingId) deleteSlide(slideEditingId);
  });

  // seg buttons
  $$('.seg-btn').forEach(b=>{
    b.addEventListener('click', ()=>setSeg(b.dataset.seg, b.dataset.val));
  });

  document.addEventListener('keydown', e=>{
    const itemOpen  = $('#modalBg').classList.contains('open');
    const catOpen   = $('#modalCatBg').classList.contains('open');
    const slideOpen = $('#modalSlideBg').classList.contains('open');
    const anyOpen = itemOpen || catOpen || slideOpen;
    if(e.key==='Escape'){
      if(slideOpen) closeSlideModal();
      else if(catOpen) closeCatModal();
      else if(itemOpen) closeModal();
    }
    if((e.ctrlKey||e.metaKey) && e.key==='Enter'){
      if(slideOpen) saveSlideModal();
      else if(catOpen) saveCat();
      else if(itemOpen) saveModal();
    }
    if(e.key==='n' && !anyOpen && !['INPUT','TEXTAREA'].includes(document.activeElement.tagName)) openModal(null);
  });

  // Sync from GitHub (if available) and then render. Async, doesn't block wiring.
  syncAndRender();
}
init();
