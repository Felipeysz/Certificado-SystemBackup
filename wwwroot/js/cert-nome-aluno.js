// ===== CERT-NOME-ALUNO.JS - Com suporte à Data de Emissão =====
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
        textAlign: document.getElementById('draggableTextAlign'),
        // ⭐ NOVOS: Elementos da data
        dataEmissaoDraggable: document.getElementById('draggableDataEmissao'),
        dataEmissaoText: document.getElementById('dataEmissaoText'),
        dataFont: document.getElementById('draggableDataFont'),
        dataFontSize: document.getElementById('draggableDataFontSize'),
        dataFontColor: document.getElementById('draggableDataFontColor'),
        dataFontWeight: document.getElementById('draggableDataFontWeight'),
        dataTextAlign: document.getElementById('draggableDataTextAlign'),
        dataFormat: document.getElementById('draggableDataFormat')
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
        position: { x: 0, y: 0 },
        // ⭐ Estado da data
        dataPosition: { x: 0, y: 0 },
        dataInitialized: false
    };

    if (el.submitBtn) el.submitBtn.disabled = true;

    // ===== FORMATAÇÃO DE DATA =====
    const formatarData = (formato) => {
        const hoje = new Date();
        const dia = String(hoje.getDate()).padStart(2, '0');
        const mes = String(hoje.getMonth() + 1).padStart(2, '0');
        const ano = hoje.getFullYear();

        const mesesExtenso = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
            'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];

        switch (formato) {
            case 'dd/MM/yyyy':
                return `${dia}/${mes}/${ano}`;
            case 'dd-MM-yyyy':
                return `${dia}-${mes}-${ano}`;
            case 'dd de MMMM de yyyy':
                return `${dia} de ${mesesExtenso[hoje.getMonth()]} de ${ano}`;
            case 'MMMM/yyyy':
                return `${mesesExtenso[hoje.getMonth()]}/${ano}`;
            default:
                return `${dia}/${mes}/${ano}`;
        }
    };

    // ===== AUTO-AJUSTE DE FONTE (Nome) =====
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

    // ===== ATUALIZAR CONTEÚDO (Nome) =====
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

    // ⭐ ATUALIZAR CONTEÚDO DA DATA
    const updateDataContent = () => {
        if (!el.dataEmissaoText) return;

        const formato = el.dataFormat?.value || 'dd/MM/yyyy';
        el.dataEmissaoText.textContent = formatarData(formato);

        Object.assign(el.dataEmissaoText.style, {
            fontFamily: el.dataFont?.value || 'Arial',
            fontSize: (el.dataFontSize?.value || 12) + 'px',
            color: el.dataFontColor?.value || '#000000',
            fontWeight: el.dataFontWeight?.checked ? 'bold' : 'normal',
            textAlign: el.dataTextAlign?.value || 'center',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis'
        });
    };

    // ===== DRAGGABLE (Nome) =====
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
        console.log('✅ Draggable (nome) inicializado');
    };

    // ⭐ DRAGGABLE DA DATA
    const initializeDataDraggable = () => {
        if (!el.dataEmissaoDraggable || !window.interact || state.dataInitialized) return;

        try {
            interact(el.dataEmissaoDraggable).unset();
        } catch (e) { }

        interact(el.dataEmissaoDraggable).draggable({
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
                    e.target.classList.add('dragging');
                    document.body.style.userSelect = 'none';
                },
                move(e) {
                    if (state.isLocked) return;

                    const x = (parseFloat(e.target.getAttribute('data-x')) || 0) + e.dx;
                    const y = (parseFloat(e.target.getAttribute('data-y')) || 0) + e.dy;

                    const transform = `translate(${x}px, ${y}px)`;
                    e.target.style.transform = transform;
                    e.target.setAttribute('data-x', x);
                    e.target.setAttribute('data-y', y);
                },
                end(e) {
                    e.target.classList.remove('dragging');
                    document.body.style.userSelect = '';

                    state.dataPosition = {
                        x: parseFloat(e.target.getAttribute('data-x')) || 0,
                        y: parseFloat(e.target.getAttribute('data-y')) || 0
                    };
                }
            }
        });

        state.dataInitialized = true;
        console.log('✅ Draggable (data) inicializado');
    };

    // ===== MOSTRAR DRAGGABLES =====
    window.showDraggableNomeAluno = () => {
        if (!el.draggable) return;

        el.draggable.style.display = 'block';

        const positionInfo = document.getElementById('positionInfo');
        if (positionInfo) {
            positionInfo.style.display = 'block';
        }

        if (!state.isInitialized) {
            el.draggable.style.transform = 'translate(0px, 0px)';
            el.draggable.setAttribute('data-x', 0);
            el.draggable.setAttribute('data-y', 0);
        }

        // ⭐ Mostrar data também
        if (el.dataEmissaoDraggable) {
            el.dataEmissaoDraggable.style.display = 'block';

            if (!state.dataInitialized) {
                el.dataEmissaoDraggable.style.transform = 'translate(0px, 0px)';
                el.dataEmissaoDraggable.setAttribute('data-x', 0);
                el.dataEmissaoDraggable.setAttribute('data-y', 0);
            }
        }

        updateContent();
        updateDataContent();

        setTimeout(() => {
            if (!state.isInitialized) initializeDraggable();
            if (!state.dataInitialized) initializeDataDraggable();
            if (!state.isLocked) autoAdjustFontSize();
        }, 150);
    };

    // ===== SALVAR CONFIG =====
    const saveConfig = () => {
        if (!el.draggable) {
            alert('Primeiro faça upload do certificado e posicione os campos.');
            return;
        }

        const rect = el.draggable.getBoundingClientRect();
        const parentRect = el.container.getBoundingClientRect();
        const fontSize = parseFloat(window.getComputedStyle(el.text).fontSize);

        const config = {
            top: Math.round(rect.top - parentRect.top) + 'px',
            left: Math.round(rect.left - parentRect.left) + 'px',
            translateX: state.position.x + 'px',
            translateY: state.position.y + 'px',
            width: Math.round(rect.width),
            height: fontSize,
            fontFamily: el.font?.value || 'Arial',
            fontSize: fontSize + 'px',
            baseFontSize: state.baseFontSize + 'px',
            color: el.fontColor?.value || '#000000',
            fontWeight: el.fontWeight?.checked ? 'bold' : 'regular',
            textAlign: el.textAlign?.value || 'center'
        };

        // ⭐ ADICIONAR CONFIG DA DATA
        if (el.dataEmissaoDraggable) {
            const dataRect = el.dataEmissaoDraggable.getBoundingClientRect();
            const dataFontSize = parseFloat(window.getComputedStyle(el.dataEmissaoText).fontSize);

            config.DataEmissao = {
                top: Math.round(dataRect.top - parentRect.top) + 'px',
                left: Math.round(dataRect.left - parentRect.left) + 'px',
                width: Math.round(dataRect.width),
                fontFamily: el.dataFont?.value || 'Arial',
                fontSize: dataFontSize + 'px',
                color: el.dataFontColor?.value || '#000000',
                fontWeight: el.dataFontWeight?.checked ? 'bold' : 'regular',
                textAlign: el.dataTextAlign?.value || 'center',
                dateFormat: el.dataFormat?.value || 'dd/MM/yyyy'
            };
        }

        el.configInput.value = JSON.stringify(config);
        state.isLocked = true;

        Object.assign(el.draggable.style, {
            borderColor: '#28a745',
            cursor: 'default'
        });

        if (el.dataEmissaoDraggable) {
            Object.assign(el.dataEmissaoDraggable.style, {
                borderColor: '#28a745',
                cursor: 'default'
            });
        }

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

        const neoContainer = document.querySelector('.neo-container');
        if (neoContainer) {
            neoContainer.insertBefore(alert, neoContainer.firstChild);
        }

        setTimeout(() => alert.remove(), 5000);

        console.log('💾 Config salva:', config);
    };

    // ===== EVENTS =====
    el.preview?.addEventListener('input', updateContent);

    [el.font, el.fontSize, el.fontColor, el.fontWeight, el.textAlign].forEach(ctrl => {
        ctrl?.addEventListener('input', updateContent);
        ctrl?.addEventListener('change', updateContent);
    });

    // ⭐ Events da data
    [el.dataFont, el.dataFontSize, el.dataFontColor, el.dataFontWeight, el.dataTextAlign, el.dataFormat].forEach(ctrl => {
        ctrl?.addEventListener('input', updateDataContent);
        ctrl?.addEventListener('change', updateDataContent);
    });

    el.saveBtn?.addEventListener('click', saveConfig);

    el.toggleDraggables?.addEventListener('change', function () {
        if (el.draggable) {
            el.draggable.style.display = this.checked ? 'block' : 'none';
        }
        if (el.dataEmissaoDraggable) {
            el.dataEmissaoDraggable.style.display = this.checked ? 'block' : 'none';
        }
    });

    el.form?.addEventListener('submit', function (e) {
        if (!el.configInput?.value) {
            e.preventDefault();
            alert('Salve a configuração dos campos antes de enviar.');
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
    el.dataEmissaoDraggable?.addEventListener('dragstart', e => e.preventDefault());

    console.log('✅ Nome Aluno + Data Module carregado');
});