// ===== CERT-PREVIEW.JS - Responsável por upload e preview de imagens/PDFs =====
document.addEventListener('DOMContentLoaded', function () {
    const el = {
        preview: document.getElementById('certificatePreview'),
        fileInput: document.querySelector('input[name="certificadoVazioFile"]'),
        img: document.getElementById('certificadoVazioImg'),
        canvas: document.getElementById('pdfPreviewCanvas'),
        placeholder: document.getElementById('previewPlaceholder')
    };

    let currentPdfTask = null;

    // ===== FUNÇÕES DE PREVIEW =====
    const resetPreview = () => {
        if (el.img) {
            el.img.src = '';
            el.img.style.display = 'none';
        }
        if (el.canvas) el.canvas.style.display = 'none';
        if (el.placeholder) el.placeholder.style.display = 'flex';

        el.preview.style.height = 'auto';
        el.preview.style.minHeight = '450px';
    };

    const adjustContainer = (width, height) => {
        el.preview.style.height = height + 'px';
        el.preview.style.minHeight = height + 'px';
        console.log(`📐 Container: ${width}x${height}px`);
    };

    const renderImage = (file) => {
        const reader = new FileReader();
        reader.onload = (e) => {
            el.img.src = e.target.result;
            el.img.style.display = 'block';
            if (el.canvas) el.canvas.style.display = 'none';

            el.img.onload = () => {
                const w = el.preview.clientWidth;
                const h = w / (el.img.naturalWidth / el.img.naturalHeight);
                adjustContainer(w, h);

                Object.assign(el.img.style, {
                    width: '100%', height: '100%', display: 'block',
                    margin: '0', padding: '0', objectFit: 'contain'
                });

                window.showDraggableNomeAluno?.();
                console.log('✅ Imagem carregada');
            };
        };
        reader.readAsDataURL(file);
    };

    const renderPDF = async (file) => {
        if (!window.pdfjsLib) {
            alert('Erro: PDF.js não carregado. Recarregue a página.');
            return;
        }

        try {
            if (currentPdfTask) {
                await currentPdfTask.cancel();
                currentPdfTask = null;
            }

            const pdf = await pdfjsLib.getDocument({ data: await file.arrayBuffer() }).promise;
            const page = await pdf.getPage(1);

            const containerWidth = el.preview.clientWidth;
            const viewport = page.getViewport({ scale: 1 });
            const scale = containerWidth / viewport.width;
            const scaledViewport = page.getViewport({ scale });

            el.canvas.width = scaledViewport.width;
            el.canvas.height = scaledViewport.height;
            adjustContainer(scaledViewport.width, scaledViewport.height);

            currentPdfTask = page.render({
                canvasContext: el.canvas.getContext('2d'),
                viewport: scaledViewport
            });

            await currentPdfTask.promise;
            currentPdfTask = null;

            Object.assign(el.canvas.style, {
                display: 'block', width: '100%', height: '100%',
                margin: '0', padding: '0'
            });

            if (el.img) el.img.style.display = 'none';
            window.showDraggableNomeAluno?.();

            console.log(`✅ PDF: ${scaledViewport.width}x${scaledViewport.height}px`);

        } catch (error) {
            if (error.name !== 'RenderingCancelledException') {
                console.error('Erro PDF:', error);
                alert('Erro ao carregar PDF.');
                resetPreview();
            }
            currentPdfTask = null;
        }
    };

    // ===== UPLOAD =====
    el.fileInput?.addEventListener('change', async (e) => {
        const file = e.target.files[0];
        if (!file) return resetPreview();

        if (el.placeholder) el.placeholder.style.display = 'none';
        file.type === 'application/pdf' ? await renderPDF(file) : renderImage(file);
    });

    // ===== RESIZE =====
    let resizeTimeout;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(async () => {
            if (el.img?.style.display !== 'none' && el.img.naturalWidth) {
                const w = el.preview.clientWidth;
                const h = w / (el.img.naturalWidth / el.img.naturalHeight);
                adjustContainer(w, h);
            } else if (el.canvas?.style.display !== 'none') {
                const file = el.fileInput?.files[0];
                if (file?.type === 'application/pdf') await renderPDF(file);
            }
        }, 250);
    });

    console.log('✅ Preview Module carregado');
});