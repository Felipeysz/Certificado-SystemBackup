// ===== CONFIG NOME ALUNO - Responsável pela configuração do nome do aluno =====
document.addEventListener('DOMContentLoaded', function () {
    // ===== ELEMENTOS DO DOM =====
    const elements = {
        form: document.getElementById('certificateForm'),
        submitBtn: document.querySelector('#certificateForm button[type="submit"]'),
        saveConfigBtn: document.getElementById('saveConfigBtn'),
        nomeAlunoPreview: document.getElementById('nomeAlunoPreview'),
        draggableNomeAluno: document.getElementById('draggableNomeAluno'),
        nomeAlunoText: document.getElementById('nomeAlunoText'),
        nomeAlunoConfigInput: document.getElementById('NomeAlunoConfig'),
        certificatePreview: document.getElementById('certificatePreview'),
        positionInfo: document.getElementById('positionInfo'),
        posX: document.getElementById('posX'),
        posY: document.getElementById('posY'),
        toggleDraggables: document.getElementById('toggleDraggables'),
        // Controles de estilo
        fontSelector: document.getElementById('draggableFont'),
        fontSizeInput: document.getElementById('draggableFontSize'),
        fontColorInput: document.getElementById('draggableFontColor'),
        fontWeightInput: document.getElementById('draggableFontWeight'),
        textAlignSelector: document.getElementById('draggableTextAlign')
    };

    // Validação de elementos essenciais
    if (!elements.form || !elements.certificatePreview) {
        console.error('Erro: elementos principais não encontrados.');
        return;
    }

    // Estado
    let isLocked = false;
    let baseFontSize = 24; // Tamanho base da fonte

    // Desabilita submit até salvar configuração
    if (elements.submitBtn) elements.submitBtn.disabled = true;

    // ===== FUNÇÃO DE AUTO-AJUSTE DE FONTE =====
    const autoAdjustFontSize = () => {
        if (!elements.nomeAlunoText || !elements.draggableNomeAluno) return;

        const containerWidth = elements.draggableNomeAluno.offsetWidth || 400;
        const text = elements.nomeAlunoText.textContent;

        // Calcula tamanho base
        baseFontSize = parseInt(elements.fontSizeInput?.value) || 24;
        let currentFontSize = baseFontSize;

        // Aplica tamanho temporário para medir
        elements.nomeAlunoText.style.fontSize = currentFontSize + 'px';
        elements.nomeAlunoText.style.whiteSpace = 'nowrap'; // Força uma linha

        let textWidth = elements.nomeAlunoText.scrollWidth;

        // Se o texto não couber, diminui a fonte progressivamente
        while (textWidth > containerWidth && currentFontSize > 8) {
            currentFontSize -= 1;
            elements.nomeAlunoText.style.fontSize = currentFontSize + 'px';
            textWidth = elements.nomeAlunoText.scrollWidth;
        }

        // Atualiza o input visual (para referência do usuário)
        if (elements.fontSizeInput && currentFontSize !== baseFontSize) {
            elements.fontSizeInput.value = currentFontSize;
        }

        console.log(`📏 Auto-ajuste: ${baseFontSize}px → ${currentFontSize}px (Largura: ${textWidth}/${containerWidth})`);
    };

    // ===== FUNÇÕES DE TEXTO =====
    const updateNomeAlunoText = () => {
        if (!elements.nomeAlunoText) return;
        elements.nomeAlunoText.textContent = elements.nomeAlunoPreview?.value || 'João da Silva';

        // Auto-ajusta após mudar o texto
        setTimeout(autoAdjustFontSize, 50);
    };

    const updateNomeAlunoStyle = () => {
        if (!elements.nomeAlunoText) return;

        const styles = {
            fontFamily: elements.fontSelector?.value || 'Arial',
            fontSize: (elements.fontSizeInput?.value || 24) + 'px',
            color: elements.fontColorInput?.value || '#000000',
            fontWeight: elements.fontWeightInput?.checked ? 'bold' : 'normal',
            textAlign: elements.textAlignSelector?.value || 'center',
            whiteSpace: 'nowrap', // ⭐ PREVINE QUEBRA DE LINHA
            overflow: 'hidden',
            textOverflow: 'ellipsis'
        };

        Object.assign(elements.nomeAlunoText.style, styles);

        // Recalcula tamanho base
        baseFontSize = parseInt(elements.fontSizeInput?.value) || 24;

        // Auto-ajusta após mudar o estilo
        setTimeout(autoAdjustFontSize, 50);
    };

    // ===== DRAGGABLE COM INTERACT.JS =====
    const initializeDraggable = () => {
        if (!elements.draggableNomeAluno || !window.interact) {
            console.warn('Interact.js não encontrado');
            return;
        }

        interact(elements.draggableNomeAluno).draggable({
            inertia: true,
            modifiers: [
                interact.modifiers.restrictRect({
                    restriction: 'parent',
                    endOnly: true
                })
            ],
            autoScroll: true,
            listeners: {
                start(event) {
                    if (isLocked) return;
                    event.target.classList.add('dragging');
                },
                move(event) {
                    if (isLocked) return;

                    const target = event.target;
                    const x = (parseFloat(target.getAttribute('data-x')) || 0) + event.dx;
                    const y = (parseFloat(target.getAttribute('data-y')) || 0) + event.dy;

                    target.style.transform = `translate(${x}px, ${y}px)`;
                    target.setAttribute('data-x', x);
                    target.setAttribute('data-y', y);

                    // Atualiza posição
                    if (elements.posX) elements.posX.textContent = Math.round(x);
                    if (elements.posY) elements.posY.textContent = Math.round(y);
                },
                end(event) {
                    event.target.classList.remove('dragging');
                }
            }
        });
    };

    // ===== MOSTRAR CAMPO DRAGGABLE =====
    window.showDraggableNomeAluno = () => {
        if (!elements.draggableNomeAluno) return;

        elements.draggableNomeAluno.style.display = 'block';
        if (elements.positionInfo) elements.positionInfo.style.display = 'block';

        updateNomeAlunoText();
        updateNomeAlunoStyle();
        initializeDraggable();

        // Auto-ajusta após exibir
        setTimeout(autoAdjustFontSize, 100);
    };

    // ===== SALVAR CONFIGURAÇÃO =====
    const saveConfiguration = () => {
        if (!elements.draggableNomeAluno) {
            alert('Primeiro faça upload do certificado e posicione o nome do aluno.');
            return;
        }

        const rect = elements.draggableNomeAluno.getBoundingClientRect();
        const parentRect = elements.certificatePreview.getBoundingClientRect();

        // ⭐ Usa o tamanho REAL da fonte (após auto-ajuste)
        const computedFontSize = window.getComputedStyle(elements.nomeAlunoText).fontSize;
        const actualFontSize = parseFloat(computedFontSize);

        const config = {
            Top: Math.round(rect.top - parentRect.top) + 'px',
            Left: Math.round(rect.left - parentRect.left) + 'px',
            Width: Math.round(rect.width),
            Height: actualFontSize,
            FontFamily: elements.fontSelector?.value || 'Arial',
            FontSize: actualFontSize + 'px', // ⭐ Salva tamanho real
            BaseFontSize: baseFontSize + 'px', // ⭐ Salva tamanho base para referência
            Color: elements.fontColorInput?.value || '#000000',
            FontWeight: elements.fontWeightInput?.checked ? 'bold' : 'regular',
            TextAlign: elements.textAlignSelector?.value || 'center'
        };

        elements.nomeAlunoConfigInput.value = JSON.stringify(config);

        // Trava o campo
        isLocked = true;
        Object.assign(elements.draggableNomeAluno.style, {
            borderColor: '#28a745',
            cursor: 'default'
        });

        // Habilita submit
        if (elements.submitBtn) elements.submitBtn.disabled = false;

        // Feedback visual
        elements.saveConfigBtn.innerHTML = '<i class="bi bi-check-circle-fill me-2"></i>Configuração Salva!';
        elements.saveConfigBtn.classList.replace('btn-outline-success', 'btn-success');
        elements.saveConfigBtn.disabled = true;

        showSuccessMessage('Configuração do nome do aluno salva com sucesso!');
        console.log('Configuração salva:', config);
    };

    // ===== MENSAGEM DE SUCESSO =====
    const showSuccessMessage = (message) => {
        const alert = document.createElement('div');
        alert.className = 'alert alert-success alert-dismissible fade show';
        alert.innerHTML = `
            <i class="bi bi-check-circle-fill me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        const container = document.querySelector('.neo-container');
        if (container) {
            container.insertBefore(alert, container.firstChild);
            setTimeout(() => alert.remove(), 5000);
        }
    };

    // ===== EVENT LISTENERS =====
    // Atualizar texto
    elements.nomeAlunoPreview?.addEventListener('input', updateNomeAlunoText);

    // Atualizar estilos
    [elements.fontSelector, elements.fontSizeInput, elements.fontColorInput,
    elements.fontWeightInput, elements.textAlignSelector].forEach(el => {
        if (el) {
            el.addEventListener('input', updateNomeAlunoStyle);
            el.addEventListener('change', updateNomeAlunoStyle);
        }
    });

    // Salvar configuração
    elements.saveConfigBtn?.addEventListener('click', saveConfiguration);

    // Toggle draggables
    elements.toggleDraggables?.addEventListener('change', function () {
        if (elements.draggableNomeAluno) {
            elements.draggableNomeAluno.style.display = this.checked ? 'block' : 'none';
        }
    });

    // Validação no submit
    elements.form?.addEventListener('submit', function (e) {
        if (!elements.nomeAlunoConfigInput?.value) {
            e.preventDefault();
            alert('Por favor, salve a configuração do nome do aluno antes de enviar o formulário.');
            return false;
        }
    });

    // ⭐ Re-ajusta ao redimensionar janela
    window.addEventListener('resize', () => {
        if (elements.draggableNomeAluno?.style.display !== 'none') {
            autoAdjustFontSize();
        }
    });

    console.log('Config Nome Aluno carregado com sucesso!');
});