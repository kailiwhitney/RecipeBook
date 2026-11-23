// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// --- Tag / Cuisine pickers (_TagPicker.cshtml) ---------------------------------
// Selected values post as hidden inputs named Field[i]. On submit we renumber
// them so there are no gaps, and the server normalizes (camping == Camping).
function pickerRoot(btn){ return btn.closest('.tag-picker'); }

function renumber(picker){
  const field = picker.dataset.field;
  picker.querySelector('.tag-hidden').innerHTML = '';
  let i = 0;
  picker.querySelectorAll('.tag-selected .badge').forEach(b => {
    const val = b.dataset.value;
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = field + '[' + (i++) + ']';
    input.value = val;
    picker.querySelector('.tag-hidden').appendChild(input);
  });
}

function selectedValues(picker){
  return Array.from(picker.querySelectorAll('.tag-selected .badge'))
              .map(b => b.dataset.value.toLowerCase());
}

function makeSelectedBadge(field, value){
  const span = document.createElement('span');
  span.className = 'badge bg-secondary d-inline-flex align-items-center';
  span.dataset.value = value;
  span.textContent = value;
  const x = document.createElement('button');
  x.type = 'button'; x.className = 'btn-close ms-1'; x.setAttribute('aria-label','remove');
  x.onclick = () => { removeTagBadge(x, value); };
  span.appendChild(x);
  return span;
}

function addSelected(picker, value){
  picker.querySelector('.tag-selected').appendChild(makeSelectedBadge(picker.dataset.field, value));
  const badge = picker.querySelector('.tag-existing .tag-badge[data-value="' + CSS.escape(value) + '"]');
  if (badge) badge.classList.remove('opacity-50');
}

function toggleTagBadge(btn){
  const picker = pickerRoot(btn);
  const value = btn.dataset.value;
  const existing = picker.querySelector('.tag-selected .badge[data-value="' + CSS.escape(value) + '"]');
  if (existing) { removeTagBadge(existing.querySelector('.btn-close'), value); }
  else { addSelected(picker, value); }
}

function removeTagBadge(btn, value){
  const picker = pickerRoot(btn);
  const badge = picker.querySelector('.tag-selected .badge[data-value="' + CSS.escape(value) + '"]');
  if (badge) badge.remove();
  const opt = picker.querySelector('.tag-existing .tag-badge[data-value="' + CSS.escape(value) + '"]');
  if (opt) opt.classList.add('opacity-50');
}

function addTagBadge(btn){
  const picker = pickerRoot(btn);
  const input = picker.querySelector('.tag-new-input');
  const raw = (input.value || '').trim();
  if (!raw) return;
  // Client-side case-insensitive dedupe mirrors the server normalizer.
  if (selectedValues(picker).includes(raw.toLowerCase())) { input.value=''; return; }
  addSelected(picker, raw);
  input.value = '';
}

// Renumber every picker right before submit so indices are contiguous.
document.addEventListener('submit', e => {
  if (!e.target || !e.target.querySelectorAll) return;
  e.target.querySelectorAll('.tag-picker').forEach(renumber);
}, true);

