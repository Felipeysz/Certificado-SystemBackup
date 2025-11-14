// ===== CERT-PDF-GENERATOR.JS - Geração e download de certificados em PDF =====
document.addEventListener('DOMContentLoaded', function () {
    const el = {
        form: document.getElementById('certificateForm'),
        preview: document.getElementById('certificatePreview'),
        fileInput: document.querySelector('input[name="certificadoVazioFile"]'),
        renderBtn: document.getElementById('renderCertificateBtn'),
        nomeAluno: document.getElementById('draggableNomeAluno'),
        dataEmissao: document.getElementById('draggableDataEmissao') // ⭐ ADICIONAR
    };

    // Criar/recuperar input hidden para Base64
    el.base64Input = document.getElementById('CertificadoGeradoBase64') || (() => {
        const input = document.createElement('input');
        Object.assign(input, {
            type: 'hidden',
            name: 'CertificadoGeradoBase64',
            id: 'CertificadoGeradoBase64'
        });
        el.form.appendChild(input);
        return input;
    })();

    // ===== ESTILOS DE EDIÇÃO =====
    const removeEditingStyles = () => {
        const saved = [];

        // Preview container
        if (el.preview) {
            const orig = {
                border: el.preview.style.border,
                outline: el.preview.style.outline,
                boxShadow: el.preview.style.boxShadow,
                background: el.preview.style.background
            };
            saved.push({ element: el.preview, styles: orig });
            Object.assign(el.preview.style, {
                border: 'none',
                outline: 'none',
                boxShadow: 'none',
                background: 'transparent'
            });
        }

        // ⭐ Remover estilos de TODOS os draggables (incluindo nome e data)
        el.preview.querySelectorAll('.draggable-field').forEach(div => {
            const orig = {
                cursor: div.style.cursor,
                padding: div.style.padding,
                background: div.style.background,
                backgroundColor: div.style.backgroundColor,
                border: div.style.border,
                borderRadius: div.style.borderRadius,
                minWidth: div.style.minWidth,
                minHeight: div.style.minHeight,
                boxShadow: div.style.boxShadow,
                outline: div.style.outline,
                display: div.style.display
            };
            saved.push({ element: div, styles: orig });

            // Adicionar classe para ajudar na captura
            div.classList.add('pdf-capture');

            Object.assign(div.style, {
                cursor: 'default',
                padding: '0',
                background: 'transparent',
                backgroundColor: 'transparent',
                border: 'none',
                borderRadius: '0',
                minWidth: 'auto',
                minHeight: 'auto',
                boxShadow: 'none',
                outline: 'none',
                display: 'none'
            });
        });

        return saved;
    };

    const restoreEditingStyles = (saved) => {
        // Remover classes de captura
        el.preview.querySelectorAll('.pdf-capture').forEach(div => {
            div.classList.remove('pdf-capture');
        });

        saved.forEach(({ element, styles }) => Object.assign(element.style, styles));
    };

    // ===== GERAÇÃO DE PDF =====
    const generatePDF = async (scale = 4) => {
        if (!window.html2canvas || !window.jspdf) {
            throw new Error('html2canvas ou jsPDF não carregados');
        }

        console.log('🔄 Iniciando geração do PDF...');
        const saved = removeEditingStyles();

        try {
            // ⭐ AGUARDAR APLICAÇÃO DOS ESTILOS
            await new Promise(resolve => setTimeout(resolve, 100));

            const canvas = await html2canvas(el.preview, {
                scale,
                useCORS: true,
                allowTaint: false,
                logging: false,
                backgroundColor: null,
                imageTimeout: 0,
                removeContainer: true,
                ignoreElements: (element) => {
                    // ⭐ IGNORAR TODOS OS ELEMENTOS DRAGGABLES
                    const isDraggable = element.classList.contains('draggable-field') ||
                        element.classList.contains('drag-handle') ||
                        element.id === 'draggableNomeAluno' ||
                        element.id === 'draggableDataEmissao';

                    if (isDraggable) {
                        console.log('🚫 Ignorando elemento:', element.id || element.className);
                    }

                    return isDraggable;
                }
            });

            const w = canvas.width / scale;
            const h = canvas.height / scale;
            const img = canvas.toDataURL('image/png', 1.0);

            const pdf = new jspdf.jsPDF({
                orientation: w > h ? 'landscape' : 'portrait',
                unit: 'pt',
                format: [w, h],
                compress: false
            });

            pdf.addImage(img, 'PNG', 0, 0, w, h, undefined, 'FAST');

            console.log('✅ PDF gerado com sucesso');
            return pdf.output('dataurlstring');

        } finally {
            restoreEditingStyles(saved);
        }
    };

    // ===== PREVIEW DOWNLOAD =====
    el.renderBtn?.addEventListener('click', async (e) => {
        e.preventDefault();

        if (!el.fileInput?.files?.length) {
            return alert('Selecione o certificado vazio antes de visualizar.');
        }

        try {
            const data = await generatePDF(4);
            const [meta, b64] = data.split(',');
            const mime = meta.match(/:(.*?);/)[1];
            const bytes = atob(b64);
            const arr = new Uint8Array(bytes.length);

            for (let i = 0; i < bytes.length; i++) arr[i] = bytes.charCodeAt(i);

            const blob = new Blob([arr], { type: mime });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = `Certificado_Preview_${new Date().toISOString().slice(0, 10)}.pdf`;
            link.click();

            setTimeout(() => URL.revokeObjectURL(link.href), 100);
            alert('✅ Preview baixado com sucesso!');
        } catch (error) {
            console.error('❌ Erro preview:', error);
            alert('Erro ao gerar preview: ' + error.message);
        }
    });

    // ===== SUBMIT =====
    el.form?.addEventListener('submit', async (e) => {
        e.preventDefault();

        const config = document.getElementById('NomeAlunoConfig');

        if (!el.fileInput?.files?.length) {
            return alert('Selecione o certificado vazio.');
        }

        if (!config?.value) {
            return alert('Salve a configuração do nome do aluno.');
        }

        try {
            const pdfData = await generatePDF(5);
            el.base64Input.value = pdfData;

            console.log('📄 PDF:', pdfData.length, 'bytes');
            console.log('📄 Config:', config.value);

            await new Promise(r => setTimeout(r, 100));
            el.form.submit();

        } catch (error) {
            console.error('❌ Erro submit:', error);
            alert('Erro ao gerar certificado: ' + error.message);
        }
    });

    // Previne Enter no formulário
    el.form?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') {
            e.preventDefault();
        }
    });

    // ===== ESTILOS ADICIONAIS PARA GARANTIR OCULTAÇÃO =====
    const style = document.createElement('style');
    style.textContent = `
        /* Estilos para impressão/captura do PDF */
        .draggable-field.pdf-capture,
        #draggableNomeAluno.pdf-capture,
        #draggableDataEmissao.pdf-capture {
            display: none !important;
            opacity: 0 !important;
            visibility: hidden !important;
        }
        
        .drag-handle {
            pointer-events: none;
        }
    `;
    document.head.appendChild(style);

    console.log('✅ PDF Generator Module carregado');
});