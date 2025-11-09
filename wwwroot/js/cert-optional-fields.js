// ===== CERT-OPTIONAL-FIELDS.JS - Gerencia campos opcionais arrastáveis =====
document.addEventListener('DOMContentLoaded', function () {
    const el = {
        preview: document.getElementById('certificatePreview'),
        toggleDraggables: document.getElementById('toggleDraggables')
    };

    // ===== FORMATAÇÃO =====
    const formatValue = (input) => {
        if (input.type === 'date' && input.value) {
            const [y, m, d] = input.value.split('-');
            return new Date(y, m - 1, d).toLocaleDateString('pt-BR');
        }
        return input.value || input.placeholder || 'Campo vazio';
    };

    // ===== DRAGGABLE =====
    const makeDraggable = (element) => {
        if (!window.interact) return;

        interact(element).draggable({
            inertia: false,
            modifiers: [
                interact.modifiers.restrictRect({
                    restriction: 'parent',
                    endOnly: false
                })
            ],
            autoScroll: false,
            listeners: {
                start(e) {
                    e.target.style.cursor = 'grabbing';
                    e.target.style.zIndex = 1000;
                    e.target.style.transition = 'none';
                },
                move(e) {
                    const x = (parseFloat(e.target.dataset.x) || 0) + e.dx;
                    const y = (parseFloat(e.target.dataset.y) || 0) + e.dy;

                    e.target.style.transform = `translate(${x}px, ${y}px)`;
                    e.target.dataset.x = x;
                    e.target.dataset.y = y;
                },
                end(e) {
                    e.target.style.cursor = 'move';
                    e.target.style.zIndex = 5;
                }
            }
        });
    };

    // ===== ATUALIZAR CAMPOS =====
    const updateFields = () => {
        el.preview.querySelectorAll('.draggable-div').forEach(d => d.remove());

        document.querySelectorAll('.draggable-input').forEach(input => {
            const checkbox = input.closest('.fade-in, .fade-in-up')?.querySelector('.show-on-certificate');
            if (!checkbox?.checked) return;

            const id = input.dataset.fieldId;
            const font = document.querySelector(`.field-font[data-field-id="${id}"]`);
            const size = document.querySelector(`.field-font-size[data-field-id="${id}"]`);
            const color = document.querySelector(`.field-color[data-field-id="${id}"]`);

            const div = document.createElement('div');
            div.className = 'draggable-div';
            div.textContent = formatValue(input);
            div.dataset.fieldId = id;
            div.dataset.isEditing = 'true';
            div.dataset.x = 0;
            div.dataset.y = 0;

            Object.assign(div.style, {
                position: 'absolute',
                top: '20px',
                left: '20px',
                fontFamily: font?.value || 'Arial',
                fontSize: (size?.value || 16) + 'px',
                color: color?.value || '#000',
                zIndex: 5,
                display: (el.toggleDraggables?.checked ?? true) ? 'block' : 'none',
                userSelect: 'none',
                whiteSpace: 'nowrap',
                cursor: 'move',
                padding: '4px 8px',
                background: 'rgba(255, 255, 0, 0.2)',
                border: '1px dashed #ffc107',
                borderRadius: '4px',
                minWidth: '50px',
                minHeight: '20px',
                transform: 'translate(0px, 0px)'
            });

            el.preview.appendChild(div);

            input.addEventListener('input', () => div.textContent = formatValue(input));

            [font, size, color].forEach(ctrl => {
                ctrl?.addEventListener('input', () => {
                    if (font) div.style.fontFamily = font.value;
                    if (size) div.style.fontSize = size.value + 'px';
                    if (color) div.style.color = color.value;
                });
            });

            makeDraggable(div);
        });
    };

    // ===== EVENTS =====
    document.querySelectorAll('.draggable-input').forEach(input => {
        input.addEventListener('input', updateFields);
        input.closest('.fade-in, .fade-in-up')?.querySelector('.show-on-certificate')
            ?.addEventListener('change', updateFields);
    });

    el.toggleDraggables?.addEventListener('change', function () {
        const display = this.checked ? 'block' : 'none';
        el.preview.querySelectorAll('.draggable-div').forEach(d => d.style.display = display);
    });

    updateFields();
    console.log('✅ Optional Fields Module carregado');
});