// ===== CERT-NOME-ALUNO.JS - Configuração do nome do aluno no certificado =====
document.addEventListener('DOMContentLoaded', function () {
    const el = {
        form: document.getElementById('certificateForm'),
        submitBtn: document.querySelector('#certificateForm button[type="submit"]'),
        saveBtn: document.getElementById('saveConfigBtn'),
        preview: document.getElementById('nomeAlunoPreview'),
        draggable: document.getElementById('draggableNomeAluno'),
        text: document.getElementById('nomeAlunoText'),
        configInput: document.getElementById('NomeAlunoConfig'),
        container: document.getElementById('certificatePreview'),
        posX: document.getElementById('posX'),
        posY: document.getElementById('posY'),
        toggleDraggables: document.getElementById('toggleDraggables'),
        font: document.getElementById('draggableFont'),
        fontSize: document.getElementById('draggableFontSize'),
        fontColor: document.getElementById('draggableFontColor'),
        fontWeight: document.getElementById('draggableFontWeight'),
        textAlign: document.getElementById('draggableTextAlign')
    };

    if (!el.form || !el.container) {
        console.error('❌ Elementos principais não encontrados.');
        return;
    }

    // ===== ESTADO =====
    let state = {
        isLocked: false,
        isDragging: false,
        isInitialized: false,
        baseFontSize: 24,
        position: { x: 0, y: 0 }
    };

    if (el.submitBtn) el.submitBtn.disabled = true;

    // ===== AUTO-AJUSTE DE FONTE =====
    const autoAdjustFontSize = () => {
        if (state.isDragging || state.isLocked || !el.text || !el.draggable) return;

        state.position = {
            x: parseFloat(el.draggable.getAttribute('data-x')) || 0,
            y: parseFloat(el.draggable.getAttribute('data-y')) || 0
        };

        const containerWidth = el.draggable.offsetWidth || 400;
        state.baseFontSize = parseInt(el.fontSize?.value) || 24;
        let currentFontSize = state.baseFontSize;

        el.text.style.fontSize = currentFontSize + 'px';
        el.text.style.whiteSpace = 'nowrap';

        let textWidth = el.text.scrollWidth;

        while (textWidth > containerWidth && currentFontSize > 8) {
            currentFontSize--;
            el.text.style.fontSize = currentFontSize + 'px';
            textWidth = el.text.scrollWidth;
        }

        if (el.fontSize && currentFontSize !== state.baseFontSize) {
            el.fontSize.value = currentFontSize;
        }

        requestAnimationFrame(() => {
            const transform = `translate(${state.position.x}px, ${state.position.y}px)`;
            el.draggable.style.transform = transform;
            el.draggable.style.webkitTransform = transform;
            el.draggable.setAttribute('data-x', state.position.x);
            el.draggable.setAttribute('data-y', state.position.y);
        });

        console.log(`📏 Font: ${state.baseFontSize}→${currentFontSize}px | Pos: ${state.position.x},${state.position.y}`);
    };

    // ===== ATUALIZAR CONTEÚDO =====
    const updateContent = () => {
        if (!el.text) return;

        if (state.isInitialized) {
            state.position = {
                x: parseFloat(el.draggable?.getAttribute('data-x')) || 0,
                y: parseFloat(el.draggable?.getAttribute('data-y')) || 0
            };
        }

        el.text.textContent = el.preview?.value || 'João da Silva';

        Object.assign(el.text.style, {
            fontFamily: el.font?.value || 'Arial',
            fontSize: (el.fontSize?.value || 24) + 'px',
            color: el.fontColor?.value || '#000000',
            fontWeight: el.fontWeight?.checked ? 'bold' : 'normal',
            textAlign: el.textAlign?.value || 'center',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis'
        });

        state.baseFontSize = parseInt(el.fontSize?.value) || 24;

        if (!state.isDragging && !state.isLocked) {
            setTimeout(autoAdjustFontSize, 50);
        }
    };

    // ===== DRAGGABLE =====
    const initializeDraggable = () => {
        if (!el.draggable || !window.interact || state.isInitialized) return;

        try {
            interact(el.draggable).unset();
        } catch (e) { }

        interact(el.draggable).draggable({
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
                    if (state.isLocked) return;

                    state.isDragging = true;
                    e.target.classList.add('dragging');
                    e.preventDefault();
                    e.stopPropagation();

                    document.body.style.userSelect = 'none';
                    e.target.style.transition = 'none';
                },
                move(e) {
                    if (state.isLocked) return;

                    const x = (parseFloat(e.target.getAttribute('data-x')) || 0) + e.dx;
                    const y = (parseFloat(e.target.getAttribute('data-y')) || 0) + e.dy;

                    const transform = `translate(${x}px, ${y}px)`;
                    e.target.style.transform = transform;
                    e.target.style.webkitTransform = transform;

                    e.target.setAttribute('data-x', x);
                    e.target.setAttribute('data-y', y);

                    if (el.posX) el.posX.textContent = Math.round(x);
                    if (el.posY) el.posY.textContent = Math.round(y);
                },
                end(e) {
                    e.target.classList.remove('dragging');
                    document.body.style.userSelect = '';
                    state.isDragging = false;

                    state.position = {
                        x: parseFloat(e.target.getAttribute('data-x')) || 0,
                        y: parseFloat(e.target.getAttribute('data-y')) || 0
                    };
                }
            }
        });

        state.isInitialized = true;
        console.log('✅ Draggable inicializado');
    };

    // ===== MOSTRAR DRAGGABLE =====
    window.showDraggableNomeAluno = () => {
        if (!el.draggable) return;

        el.draggable.style.display = 'block';
        document.getElementById('positionInfo')?.style.display = 'block';

        if (!state.isInitialized) {
            el.draggable.style.transform = 'translate(0px, 0px)';
            el.draggable.setAttribute('data-x', 0);
            el.draggable.setAttribute('data-y', 0);
        }

        updateContent();

        setTimeout(() => {
            if (!state.isInitialized) initializeDraggable();
            if (!state.isLocked) autoAdjustFontSize();
        }, 150);
    };

    // ===== SALVAR CONFIG =====
    const saveConfig = () => {
        if (!el.draggable) {
            alert('Primeiro faça upload do certificado e posicione o nome do aluno.');
            return;
        }

        const rect = el.draggable.getBoundingClientRect();
        const parentRect = el.container.getBoundingClientRect();
        const fontSize = parseFloat(window.getComputedStyle(el.text).fontSize);

        const config = {
            Top: Math.round(rect.top - parentRect.top) + 'px',
            Left: Math.round(rect.left - parentRect.left) + 'px',
            TranslateX: state.position.x + 'px',
            TranslateY: state.position.y + 'px',
            Width: Math.round(rect.width),
            Height: fontSize,
            FontFamily: el.font?.value || 'Arial',
            FontSize: fontSize + 'px',
            BaseFontSize: state.baseFontSize + 'px',
            Color: el.fontColor?.value || '#000000',
            FontWeight: el.fontWeight?.checked ? 'bold' : 'regular',
            TextAlign: el.textAlign?.value || 'center'
        };

        el.configInput.value = JSON.stringify(config);
        state.isLocked = true;

        Object.assign(el.draggable.style, {
            borderColor: '#28a745',
            cursor: 'default'
        });

        if (el.submitBtn) el.submitBtn.disabled = false;

        el.saveBtn.innerHTML = '<i class="bi bi-check-circle-fill me-2"></i>Configuração Salva!';
        el.saveBtn.classList.replace('btn-outline-success', 'btn-success');
        el.saveBtn.disabled = true;

        const alert = document.createElement('div');
        alert.className = 'alert alert-success alert-dismissible fade show';
        alert.innerHTML = `
            <i class="bi bi-check-circle-fill me-2"></i>Configuração salva com sucesso!
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.querySelector('.neo-container')?.insertBefore(alert, document.querySelector('.neo-container').firstChild);
        setTimeout(() => alert.remove(), 5000);

        console.log('💾 Config salva:', config);
    };

    // ===== EVENTS =====
    el.preview?.addEventListener('input', updateContent);

    [el.font, el.fontSize, el.fontColor, el.fontWeight, el.textAlign].forEach(ctrl => {
        ctrl?.addEventListener('input', updateContent);
        ctrl?.addEventListener('change', updateContent);
    });

    el.saveBtn?.addEventListener('click', saveConfig);

    el.toggleDraggables?.addEventListener('change', function () {
        if (el.draggable) {
            el.draggable.style.display = this.checked ? 'block' : 'none';
        }
    });

    el.form?.addEventListener('submit', function (e) {
        if (!el.configInput?.value) {
            e.preventDefault();
            alert('Salve a configuração do nome do aluno antes de enviar.');
            return false;
        }
    });

    // Resize otimizado
    let resizeTimeout, lastWidth = window.innerWidth;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimeout);
        if (Math.abs(window.innerWidth - lastWidth) < 10) return;
        lastWidth = window.innerWidth;

        resizeTimeout = setTimeout(() => {
            if (el.draggable?.style.display !== 'none' && !state.isDragging && !state.isLocked) {
                autoAdjustFontSize();
            }
        }, 300);
    });

    el.draggable?.addEventListener('dragstart', e => e.preventDefault());

    console.log('✅ Nome Aluno Module carregado');
});