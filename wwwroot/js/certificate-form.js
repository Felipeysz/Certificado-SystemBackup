// ===== CERTIFICATE FORM - Responsável por upload, preview, campos opcionais e geração =====
document.addEventListener('DOMContentLoaded', function () {
    // ===== ELEMENTOS DO DOM =====
    const elements = {
        form: document.getElementById('certificateForm'),
        certificatePreview: document.getElementById('certificatePreview'),
        certificadoVazioInput: document.querySelector('input[name="certificadoVazioFile"]'),
        certificadoVazioImg: document.getElementById('certificadoVazioImg'),
        pdfPreviewCanvas: document.getElementById('pdfPreviewCanvas'),
        previewPlaceholder: document.getElementById('previewPlaceholder'),
        renderBtn: document.getElementById('renderCertificateBtn'),
        toggleDraggablesCheckbox: document.getElementById('toggleDraggables'),
        certificadoBase64Input: document.getElementById('CertificadoGeradoBase64'),
        draggableNomeAluno: document.getElementById('draggableNomeAluno')
    };

    // Criar input hidden se não existir
    if (!elements.certificadoBase64Input) {
        elements.certificadoBase64Input = document.createElement('input');
        Object.assign(elements.certificadoBase64Input, {
            type: 'hidden',
            name: 'CertificadoGeradoBase64',
            id: 'CertificadoGeradoBase64'
        });
        elements.form.appendChild(elements.certificadoBase64Input);
    }

    // Variável para controlar renderização de PDF
    let currentRenderTask = null;

    // ===== FUNÇÕES DE PREVIEW =====
    const resetPreview = () => {
        if (elements.certificadoVazioImg) {
            elements.certificadoVazioImg.src = '';
            elements.certificadoVazioImg.style.display = 'none';
        }
        if (elements.pdfPreviewCanvas) {
            elements.pdfPreviewCanvas.style.display = 'none';
        }
        if (elements.previewPlaceholder) {
            elements.previewPlaceholder.style.display = 'flex';
        }
    };

    const adjustImageSize = () => {
        const containerWidth = elements.certificatePreview.clientWidth;
        const containerHeight = elements.certificatePreview.clientHeight;
        const imgRatio = elements.certificadoVazioImg.naturalWidth / elements.certificadoVazioImg.naturalHeight;
        const containerRatio = containerWidth / containerHeight;

        const [newWidth, newHeight] = imgRatio > containerRatio
            ? [containerWidth, containerWidth / imgRatio]
            : [containerHeight * imgRatio, containerHeight];

        Object.assign(elements.certificadoVazioImg.style, {
            width: newWidth + 'px',
            height: newHeight + 'px',
            objectFit: 'contain',
            maxWidth: '100%',
            maxHeight: '100%',
            display: 'block',
            margin: '0 auto'
        });
    };

    // ===== RENDERIZAR PDF =====
    const renderPDF = async (file) => {
        if (!window.pdfjsLib) {
            console.error('PDF.js não está carregado');
            alert('Erro ao carregar PDF. Recarregue a página.');
            return;
        }

        try {
            // Cancela renderização anterior
            if (currentRenderTask) {
                await currentRenderTask.cancel();
                currentRenderTask = null;
            }

            const arrayBuffer = await file.arrayBuffer();
            const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
            const page = await pdf.getPage(1);

            // Ajusta escala
            const containerWidth = elements.certificatePreview.clientWidth - 32;
            const viewport = page.getViewport({ scale: 1 });
            const scale = containerWidth / viewport.width;
            const scaledViewport = page.getViewport({ scale });

            const canvas = elements.pdfPreviewCanvas;
            const context = canvas.getContext('2d');

            canvas.height = scaledViewport.height;
            canvas.width = scaledViewport.width;

            // Renderiza
            currentRenderTask = page.render({
                canvasContext: context,
                viewport: scaledViewport
            });

            await currentRenderTask.promise;
            currentRenderTask = null;

            // Exibe canvas
            canvas.style.display = 'block';
            if (elements.certificadoVazioImg) elements.certificadoVazioImg.style.display = 'none';

            // Chama função global do outro script
            if (typeof window.showDraggableNomeAluno === 'function') {
                window.showDraggableNomeAluno();
            }

            console.log('PDF renderizado com sucesso');

        } catch (error) {
            if (error.name !== 'RenderingCancelledException') {
                console.error('Erro ao renderizar PDF:', error);
                alert('Erro ao carregar PDF. Tente outro arquivo.');
                resetPreview();
            }
            currentRenderTask = null;
        }
    };

    // ===== RENDERIZAR IMAGEM =====
    const renderImage = (file) => {
        const reader = new FileReader();

        reader.onload = (e) => {
            elements.certificadoVazioImg.src = e.target.result;
            elements.certificadoVazioImg.style.display = 'block';
            if (elements.pdfPreviewCanvas) elements.pdfPreviewCanvas.style.display = 'none';

            elements.certificadoVazioImg.onload = () => {
                adjustImageSize();

                // Chama função global do outro script
                if (typeof window.showDraggableNomeAluno === 'function') {
                    window.showDraggableNomeAluno();
                }

                console.log('Imagem carregada com sucesso');
            };
        };

        reader.readAsDataURL(file);
    };

    // ===== UPLOAD DO CERTIFICADO =====
    elements.certificadoVazioInput?.addEventListener('change', async function (e) {
        const file = e.target.files[0];

        if (!file) {
            resetPreview();
            return;
        }

        if (elements.previewPlaceholder) elements.previewPlaceholder.style.display = 'none';

        file.type === 'application/pdf' ? await renderPDF(file) : renderImage(file);
    });

    // ===== CAMPOS OPCIONAIS ARRASTÁVEIS =====
    const formatFieldValue = (input) => {
        let value = input.value;

        if (input.type === 'date' && value) {
            const [year, month, day] = value.split('-');
            return new Date(year, month - 1, day).toLocaleDateString('pt-BR');
        }

        return value || input.placeholder || 'Campo vazio';
    };

    const makeDraggable = (element) => {
        let isDragging = false;
        let offsetX = 0, offsetY = 0;

        element.addEventListener('mousedown', (e) => {
            isDragging = true;
            offsetX = e.offsetX;
            offsetY = e.offsetY;
            element.style.cursor = 'grabbing';
            element.style.zIndex = 1000;
        });

        document.addEventListener('mousemove', (e) => {
            if (!isDragging) return;

            const rect = elements.certificatePreview.getBoundingClientRect();
            let left = Math.max(0, Math.min(
                e.pageX - rect.left - offsetX - window.pageXOffset,
                rect.width - element.offsetWidth
            ));
            let top = Math.max(0, Math.min(
                e.pageY - rect.top - offsetY - window.pageYOffset,
                rect.height - element.offsetHeight
            ));

            element.style.left = left + 'px';
            element.style.top = top + 'px';
        });

        document.addEventListener('mouseup', () => {
            if (isDragging) {
                isDragging = false;
                element.style.cursor = 'move';
                element.style.zIndex = 5;
            }
        });
    };

    const updateDraggableDivs = () => {
        // Remove divs antigas
        elements.certificatePreview.querySelectorAll('.draggable-div').forEach(d => d.remove());

        // Cria novas divs
        document.querySelectorAll('.draggable-input').forEach(input => {
            const checkbox = input.closest('.fade-in, .fade-in-up')?.querySelector('.show-on-certificate');
            if (!checkbox?.checked) return;

            const fieldId = input.dataset.fieldId;
            const fontSelect = document.querySelector(`.field-font[data-field-id="${fieldId}"]`);
            const fontSizeInput = document.querySelector(`.field-font-size[data-field-id="${fieldId}"]`);
            const colorInput = document.querySelector(`.field-color[data-field-id="${fieldId}"]`);

            const div = document.createElement('div');
            div.className = 'draggable-div';
            div.innerText = formatFieldValue(input);
            div.dataset.fieldId = fieldId;

            // Estilos base (sempre aplicados)
            const baseStyles = {
                position: 'absolute',
                top: '20px',
                left: '20px',
                fontFamily: fontSelect?.value || 'Arial, sans-serif',
                fontSize: (fontSizeInput?.value || 16) + 'px',
                color: colorInput?.value || '#000',
                zIndex: 5,
                display: (elements.toggleDraggablesCheckbox?.checked ?? true) ? 'block' : 'none',
                userSelect: 'none',
                whiteSpace: 'nowrap'
            };

            // Estilos de edição (removidos na captura)
            const editStyles = {
                cursor: 'move',
                padding: '4px 8px',
                background: 'rgba(255, 255, 0, 0.2)',
                border: '1px dashed #ffc107',
                borderRadius: '4px',
                minWidth: '50px',
                minHeight: '20px'
            };

            Object.assign(div.style, baseStyles, editStyles);
            div.dataset.isEditing = 'true';

            elements.certificatePreview.appendChild(div);

            // Atualiza valor em tempo real
            input.addEventListener('input', () => {
                div.innerText = formatFieldValue(input);
            });

            // Atualiza estilo
            [fontSelect, fontSizeInput, colorInput].forEach(el => {
                el?.addEventListener('input', () => {
                    if (fontSelect) div.style.fontFamily = fontSelect.value;
                    if (fontSizeInput) div.style.fontSize = fontSizeInput.value + 'px';
                    if (colorInput) div.style.color = colorInput.value;
                });
            });

            makeDraggable(div);
        });
    };

    // ===== FUNÇÕES PARA REMOVER/RESTAURAR ESTILOS DE EDIÇÃO =====
    const removeEditingStyles = () => {
        const savedStyles = [];

        // ⭐ ESCONDE COMPLETAMENTE O NOME DO ALUNO (não apenas estilos)
        if (elements.draggableNomeAluno) {
            const originalDisplay = elements.draggableNomeAluno.style.display;
            savedStyles.push({
                element: elements.draggableNomeAluno,
                styles: { display: originalDisplay }
            });
            elements.draggableNomeAluno.style.display = 'none';
        }

        // Remove estilos dos campos opcionais arrastáveis
        const draggableDivs = elements.certificatePreview.querySelectorAll('.draggable-div');
        draggableDivs.forEach(div => {
            if (div.dataset.isEditing === 'true') {
                const originalStyles = {
                    cursor: div.style.cursor,
                    padding: div.style.padding,
                    background: div.style.background,
                    border: div.style.border,
                    borderRadius: div.style.borderRadius,
                    minWidth: div.style.minWidth,
                    minHeight: div.style.minHeight
                };
                savedStyles.push({ element: div, styles: originalStyles });

                Object.assign(div.style, {
                    cursor: 'default',
                    padding: '0',
                    background: 'transparent',
                    border: 'none',
                    borderRadius: '0',
                    minWidth: 'auto',
                    minHeight: 'auto'
                });
            }
        });

        return savedStyles;
    };

    const restoreEditingStyles = (savedStyles) => {
        savedStyles.forEach(({ element, styles }) => {
            Object.assign(element.style, styles);
        });
    };

    // ===== GERAÇÃO DO CERTIFICADO EM PDF (ALTA QUALIDADE) =====
    const generateCertificatePDF = async (scale = 4) => {
        if (!window.html2canvas || !window.jspdf) {
            throw new Error('html2canvas ou jsPDF não estão carregados');
        }

        // Remove estilos de edição e ESCONDE o nome do aluno
        const savedStyles = removeEditingStyles();

        try {
            // Captura como imagem em ALTA RESOLUÇÃO
            const canvas = await html2canvas(elements.certificatePreview, {
                scale: scale, // 4x = 4x a resolução original
                useCORS: true,
                allowTaint: false,
                logging: false,
                backgroundColor: null,
                imageTimeout: 0,
                removeContainer: true
            });

            // Dimensões em pontos (72 DPI)
            const imgWidth = canvas.width / scale;
            const imgHeight = canvas.height / scale;

            // Converte para PDF mantendo proporções reais
            const imgData = canvas.toDataURL('image/png', 1.0); // Qualidade máxima

            const pdf = new jspdf.jsPDF({
                orientation: imgWidth > imgHeight ? 'landscape' : 'portrait',
                unit: 'pt',
                format: [imgWidth, imgHeight],
                compress: false // Sem compressão para máxima qualidade
            });

            pdf.addImage(imgData, 'PNG', 0, 0, imgWidth, imgHeight, undefined, 'FAST');

            return pdf.output('dataurlstring');

        } finally {
            // Restaura estilos de edição
            restoreEditingStyles(savedStyles);
        }
    };

    // ===== PREVIEW DO CERTIFICADO (Download PDF) =====
    elements.renderBtn?.addEventListener('click', async function (e) {
        e.preventDefault();

        if (!elements.certificadoVazioInput?.files?.length) {
            alert('Selecione o certificado vazio antes de visualizar.');
            return;
        }

        try {
            const pdfData = await generateCertificatePDF(4); // Scale 4 para alta qualidade

            // Converte data URL para blob
            const arr = pdfData.split(',');
            const mime = arr[0].match(/:(.*?);/)[1];
            const bstr = atob(arr[1]);
            let n = bstr.length;
            const u8arr = new Uint8Array(n);
            while (n--) {
                u8arr[n] = bstr.charCodeAt(n);
            }
            const blob = new Blob([u8arr], { type: mime });

            // Cria link de download
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = `Certificado_Preview_${new Date().toISOString().slice(0, 10)}.pdf`;
            link.click();

            // Limpa URL objeto
            setTimeout(() => URL.revokeObjectURL(link.href), 100);

            alert('Preview do certificado baixado com sucesso em PDF!');
        } catch (error) {
            console.error('Erro ao gerar preview:', error);
            alert('Erro ao gerar preview do certificado: ' + error.message);
        }
    });

    // ===== SUBMIT DO FORMULÁRIO =====
    elements.form?.addEventListener('submit', async function (e) {
        e.preventDefault();

        const nomeAlunoConfigInput = document.getElementById('NomeAlunoConfig');

        if (!elements.certificadoVazioInput?.files?.length) {
            alert('Selecione o certificado vazio antes de enviar.');
            return;
        }

        if (!nomeAlunoConfigInput?.value) {
            alert('Salve a configuração do nome do aluno antes de enviar o formulário.');
            return;
        }

        try {
            const pdfBase64 = await generateCertificatePDF(5);
            elements.certificadoBase64Input.value = pdfBase64;

            // ⭐ LOG DETALHADO
            console.log('📄 PDF Base64 Length:', pdfBase64.length);
            console.log('📄 Primeiros 100 chars:', pdfBase64.substring(0, 100));
            console.log('📄 Config:', nomeAlunoConfigInput.value);
            console.log('📄 Campo hidden existe?', elements.certificadoBase64Input !== null);
            console.log('📄 Valor foi setado?', elements.certificadoBase64Input.value.length > 0);

            await new Promise(resolve => setTimeout(resolve, 100));

            elements.form.submit();

        } catch (error) {
            console.error('❌ Erro ao gerar certificado:', error);
            alert('Erro ao gerar certificado. Tente novamente: ' + error.message);
        }
    });



    // ===== EVENT LISTENERS =====
    // Atualizar divs arrastáveis
    document.querySelectorAll('.draggable-input').forEach(input => {
        input.addEventListener('input', updateDraggableDivs);
        const checkbox = input.closest('.fade-in, .fade-in-up')?.querySelector('.show-on-certificate');
        checkbox?.addEventListener('change', updateDraggableDivs);
    });

    // Toggle campos arrastáveis
    elements.toggleDraggablesCheckbox?.addEventListener('change', function () {
        const display = this.checked ? 'block' : 'none';
        elements.certificatePreview.querySelectorAll('.draggable-div').forEach(d => {
            d.style.display = display;
        });
    });

    // Prevenir submit com Enter
    elements.form?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') {
            e.preventDefault();
        }
    });

    // Ajustar imagem no resize
    window.addEventListener('resize', () => {
        if (elements.certificadoVazioImg?.style.display !== 'none') {
            adjustImageSize();
        }
    });

    // Inicializa divs arrastáveis
    updateDraggableDivs();

    console.log('Certificate Form carregado com sucesso!');
});