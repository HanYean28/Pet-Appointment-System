// Standard crop output size (matches .standard-image height)
const CROPPED_OUTPUT_WIDTH = 400;
const CROPPED_OUTPUT_HEIGHT = 400;

// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported) — updated to include antiforgery token and confirmation
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    if (!url) return;

    // optional confirmation to avoid accidental deletes
    if (!confirm('Are you sure you want to delete this record?')) return;

    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url;

    // If an antiforgery token exists on the page, copy it into the dynamically-created form
    const af = document.querySelector('input[name="__RequestVerificationToken"]');
    if (af && af.value) {
        const token = document.createElement('input');
        token.type = 'hidden';
        token.name = '__RequestVerificationToken';
        token.value = af.value;
        f.appendChild(token);
    }

    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase (can use for voucher codes)
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;

});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;

    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

//Photo preview function 
function handleFilePreview(file, inputElement) {
    const img = document.getElementById('photoPreview');

    img.dataset.src ??= img.src;
    if (file && file.type.startsWith('image/')) {
        img.onload = () => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(file);
        $('#photoName').text(file.name);
    }
    else {
        img.src = img.dataset.src;
        inputElement.value = '';
        $('#photoName').text('Select Photo...');
    }

    $(inputElement).valid();
}


$('.upload input').on('change', e => {
    const f = e.target.files[0];
    handleFilePreview(f, e.target);
});

//Drag and drop images functionality
const dropZone = document.getElementById('dropZone');
const fileInput = document.querySelector('#dropZone input[type="file"]');

//1.Prevent default behavior for drag events
['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
    if (dropZone) dropZone.addEventListener(eventName, preventDefaults, false);
});

function preventDefaults(e) {
    e.preventDefault();
    e.stopPropagation();
}

//2.Add visual feedback when file is dragged over
['dragenter', 'dragover'].forEach(eventName => {
    if (dropZone) dropZone.addEventListener(eventName, highlight, false);
});

['dragleave', 'drop'].forEach(eventName => {
    if (dropZone) dropZone.addEventListener(eventName, unhighlight, false);
});

function highlight() {
    dropZone.classList.add('dragover');
}

function unhighlight() {
    dropZone.classList.remove('dragover');
}

// 3.Handle the dropped files
if (dropZone) dropZone.addEventListener('drop', handleDrop, false);

function handleDrop(e) {
    const dt = e.dataTransfer;
    const files = dt.files;

    if (files.length > 0) {
        //Get the first file only
        const file = files[0];

        //Image validation
        if (!file.type.match('image/jpeg') && !file.type.match('image/png')) {
            alert('Please select a JPEG or PNG image.');
            return;
        }

        //Assign the file to the input
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        if (fileInput) fileInput.files = dataTransfer.files;

        handleFilePreview(file, fileInput);

        const event = new Event('change', { bubbles: true });
        if (fileInput) fileInput.dispatchEvent(event);
    }
}

//Photo Processing 
$.validator.setDefaults({ ignore: '' });

let cropper;
let originalFile;
let flipX = 1;
let flipY = 1;

//file selection
const photoInputEl = document.getElementById('photoInput');
if (photoInputEl) {
    photoInputEl.addEventListener('change', function (e) {
        const file = e.target.files[0];
        if (file && /^image\//.test(file.type)) {
            originalFile = file;
            const reader = new FileReader();
            reader.onload = function (evt) {
                $('#cropImage').attr('src', evt.target.result);
                $('#cropModal').css('display', 'flex');
                // Destroy previous cropper if exists
                if (cropper) cropper.destroy();
                flipX = 1;
                flipY = 1;
                cropper = new Cropper(document.getElementById('cropImage'), {
                    aspectRatio: CROPPED_OUTPUT_WIDTH / CROPPED_OUTPUT_HEIGHT,
                    viewMode: 1,
                    minCropBoxWidth: 200,
                    minCropBoxHeight: 200,
                    autoCropArea: 0.8,
                    responsive: true,
                    background: false
                });
            };
            reader.readAsDataURL(file);
        }
    });
}

//Replace the existing '#cropBtn' click handler with the one below.
// This handler writes the cropped blob back into the single-photo input (`photoInput`)
// so the original selected file is removed and only the cropped file is submitted.
// It also clears `originalFile` and destroys the cropper.

$('#cropBtn').on('click', function () {
    if (!cropper) return;

    // Check crop box size before allowing crop
    const cropData = cropper.getData(true);
    const minWidth = 200;
    const minHeight = 200;
    if (cropData.width < minWidth || cropData.height < minHeight) {
        alert('Please select an area at least 200x200 pixels.');
        return;
    }

    // Produce fixed-dimension cropped output that matches display aspect
    cropper.getCroppedCanvas({ width: CROPPED_OUTPUT_WIDTH, height: CROPPED_OUTPUT_HEIGHT, fillColor: '#fff' }).toBlob(function (blob) {
        if (!blob) return;

        // Update preview immediately
        const url = URL.createObjectURL(blob);
        $('#photoPreview').attr('src', url);

        // Build a File from the blob so we can put it into the file input
        const origName = originalFile?.name;
        const fname = origName ? ('cropped_' + origName) : ('cropped_' + Date.now() + '.jpg');
        const newFile = new File([blob], fname, { type: blob.type });

        try {
            // If a photos/multi input was targeted by the delegated flow, that code
            // in the photosInput handler will replace the file there. We try to replace
            // the single-photo input here so the original selected file is removed.
            if (typeof photoInputEl !== 'undefined' && photoInputEl instanceof HTMLInputElement) {
                const dt = new DataTransfer();
                dt.items.add(newFile);
                photoInputEl.files = dt.files;

                // Trigger a change so any per-page UI updates (photo name, validation) run
                const evt = new Event('change', { bubbles: true });
                photoInputEl.dispatchEvent(evt);
            }
        } catch (e) {
            // Non-fatal: if we couldn't set the input files for any reason, continue,
            // the base64 hidden field will still be submitted so server can save cropped image.
            console.warn('Could not replace file input with cropped file:', e);
        }

        // Convert blob to base64 for form post (keeps backward compatibility)
        const reader = new FileReader();
        reader.onloadend = function () {
            $('#CroppedPhoto').val(reader.result);
            $('#cropModal').hide();

            // Destroy cropper and clear originalFile to ensure original isn't used later
            try { cropper.destroy(); } catch { }
            cropper = null;
            originalFile = null;
        };
        reader.readAsDataURL(blob);
    }, 'image/jpeg');
});

// Replace existing skipCropBtn handler with behavior that preserves original file into the target input when skipping
$('#skipCropBtn').on('click', function () {
    // If cropper exists we prefer producing a cropped image, but allow skipping to original
    if (cropper) {
        const cropData = cropper.getData(true);
        const minWidth = 200;
        const minHeight = 200;

        if (cropData.width < minWidth || cropData.height < minHeight) {
            // If user wants to skip cropping, accept the original file (if available)
            if (typeof cropTargetInputId !== 'undefined' && cropTargetInputId) {
                const input = document.getElementById(cropTargetInputId);
                if (input && input instanceof HTMLInputElement) {
                    // Replace the File at cropTargetIndex in the input.files via DataTransfer
                    const dtReplace = new DataTransfer();
                    const oldFiles = input.files ? Array.from(input.files) : [];
                    let replaced = false;
                    for (let i = 0; i < oldFiles.length; i++) {
                        if (i === cropTargetIndex && originalFile) {
                            dtReplace.items.add(originalFile);
                            replaced = true;
                        } else {
                            dtReplace.items.add(oldFiles[i]);
                        }
                    }
                    if (!replaced && originalFile) dtReplace.items.add(originalFile);
                    input.files = dtReplace.files;
                    // trigger change so UI code (per-page dt) updates preview
                    const evt = new Event('change', { bubbles: true });
                    input.dispatchEvent(evt);
                    // clear hidden base64 field
                    const croppedHidden = document.getElementById('CroppedPhoto');
                    if (croppedHidden) croppedHidden.value = '';
                    $('#cropModal').hide();
                } else if (originalFile) {
                    // fallback for single-photo path
                    const reader = new FileReader();
                    reader.onload = function (evt) {
                        $('#photoPreview').attr('src', evt.target.result);
                        $('#CroppedPhoto').val('');
                        $('#cropModal').hide();
                    };
                    reader.readAsDataURL(originalFile);
                } else {
                    alert('No original image available to keep.');
                }
            } else if (originalFile) {
                // no target input (single-photo path) — show original
                const reader = new FileReader();
                reader.onload = function (evt) {
                    $('#photoPreview').attr('src', evt.target.result);
                    $('#CroppedPhoto').val('');
                    $('#cropModal').hide();
                };
                reader.readAsDataURL(originalFile);
            } else {
                // No original file to fall back to — keep existing validation message
                alert('Please select an area at least 200x200 pixels or click Cancel to keep the original.');
            }

            // destroy cropper and reset
            try { cropper.destroy(); } catch (e) { /* ignore */ }
            cropper = null;
            cropTargetInputId = null;
            cropTargetIndex = 0;
            return;
        }

        // If crop box is large enough, produce the cropped output as before
        cropper.getCroppedCanvas({ width: CROPPED_OUTPUT_WIDTH, height: CROPPED_OUTPUT_HEIGHT, fillColor: '#fff' }).toBlob(function (blob) {
            let url = URL.createObjectURL(blob);
            $('#photoPreview').attr('src', url);
            let reader = new FileReader();
            reader.onloadend = function () {
                $('#CroppedPhoto').val(reader.result);
                $('#cropModal').hide();
            };
            reader.readAsDataURL(blob);
        }, 'image/jpeg');
    } else {
        // no cropper: if we have originalFile, show it; otherwise nothing to do
        if (originalFile) {
            let reader = new FileReader();
            reader.onload = function (evt) {
                $('#photoPreview').attr('src', evt.target.result);
                $('#CroppedPhoto').val("");
                $('#cropModal').hide();
            };
            reader.readAsDataURL(originalFile);
        } else {
            $('#cropModal').hide();
        }
    }
});

//Display photo name
$('#photoInput').on('change', function () {
    $('#photoName').text(this.files[0]?.name || '@Localizer["Select Photo"]...');
});

//Flip and rotate
$('#rotateLeftBtn').on('click', function () {
    if (cropper) cropper.rotate(-90);
});
$('#rotateRightBtn').on('click', function () {
    if (cropper) cropper.rotate(90);
});
$('#flipHorizontalBtn').on('click', function () {
    if (cropper) {
        flipX = flipX === 1 ? -1 : 1;
        cropper.scaleX(flipX);
    }
});
$('#flipVerticalBtn').on('click', function () {
    if (cropper) {
        flipY = flipY === 1 ? -1 : 1;
        cropper.scaleY(flipY);
    }
});

// --- Webcam Capture Logic ---
// Show webcam modal on button click
$('#webcamBtn').on('click', function () {
    $('#webcamModal').show();
    // Start webcam
    navigator.mediaDevices.getUserMedia({ video: true })
        .then(function (stream) {
            webcamStream = stream;
            const video = document.getElementById('webcamVideo');
            video.srcObject = stream;
            video.play();
        })
        .catch(function (err) {
            alert('Cannot access webcam: ' + err);
            $('#webcamModal').hide();
        });
});

// Close webcam modal and stop stream
$('#closeWebcamBtn').on('click', function () {
    $('#webcamModal').hide();
    if (webcamStream) {
        webcamStream.getTracks().forEach(track => track.stop());
        webcamStream = null;
    }
});

// On "Capture", take photo and show crop modal
$('#captureBtn').on('click', function () {
    const video = document.getElementById('webcamVideo');
    const canvas = document.createElement('canvas');
    // produce canvas matching desired output aspect/size (improves crop quality)
    canvas.width = CROPPED_OUTPUT_WIDTH;
    canvas.height = CROPPED_OUTPUT_HEIGHT;
    const ctx = canvas.getContext('2d');
    // draw video scaled to canvas (may stretch depending on source aspect)
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    // Convert canvas to dataURL and show crop modal with it
    const dataUrl = canvas.toDataURL('image/jpeg');
    $('#cropImage').attr('src', dataUrl);
    $('#webcamModal').hide();

    // Stop webcam
    if (webcamStream) {
        webcamStream.getTracks().forEach(track => track.stop());
        webcamStream = null;
    }

    // Show crop modal and init cropper
    $('#cropModal').css('display', 'flex');
    if (cropper) cropper.destroy();
    cropper = new Cropper(document.getElementById('cropImage'), {
        aspectRatio: CROPPED_OUTPUT_WIDTH / CROPPED_OUTPUT_HEIGHT,
        viewMode: 1,
        minCropBoxWidth: 200,
        minCropBoxHeight: 200,
        autoCropArea: 0.8,
        responsive: true,
        background: false
    });
});
// Wire photosInput delegation and crop replacement logic remains as before (it sets originalFile when photosInput opens the cropper)
(function () {
    // Track the input we want to replace after cropping
    let cropTargetInputId = null; // e.g. 'photosInput'
    let cropTargetIndex = 0; // index within the FileList to replace

    // When a photosInput change occurs and there's exactly one file selected,
    // delegate to the existing crop modal by setting the #cropImage src and showing the modal.
    document.addEventListener('change', function (ev) {
        const t = ev.target;
        if (!t || !(t instanceof HTMLInputElement)) return;
        if (t.id !== 'photosInput') return;
        const files = t.files;
        if (!files || files.length === 0) return;

        // Only trigger crop UI for a single selection — adjust as needed for multi-crop UX
        if (files.length === 1) {
            const file = files[0];
            if (!/^image\//.test(file.type)) return;

            const reader = new FileReader();
            reader.onload = function (e) {
                // Set crop target so crop button handler can replace the file
                cropTargetInputId = t.id;
                cropTargetIndex = 0;

                // remember original file so Skip can fall back to it
                originalFile = file;

                // Populate crop modal image
                const cropImg = document.getElementById('cropImage');
                if (cropImg) {
                    cropImg.src = e.target.result;
                }

                // Show modal (matches existing CSS)
                const modal = document.getElementById('cropModal');
                if (modal) modal.style.display = 'flex';

                // Initialize cropper (use same aspect as output)
                if (cropper) cropper.destroy();
                cropper = new Cropper(document.getElementById('cropImage'), {
                    aspectRatio: CROPPED_OUTPUT_WIDTH / CROPPED_OUTPUT_HEIGHT,
                    viewMode: 1,
                    minCropBoxWidth: 200,
                    minCropBoxHeight: 200,
                    autoCropArea: 0.8,
                    responsive: true,
                    background: false
                });
            };
            reader.readAsDataURL(file);
        }
    });

    // Hook the crop button to replace the target input file with the cropped result.
    document.addEventListener('click', function (ev) {
        const el = ev.target;
        if (!el || !(el instanceof HTMLElement)) return;
        if (el.id !== 'cropBtn') return;

        // Find the cropper instance (the project uses a global variable `cropper` in app.js)
        if (typeof cropper === 'undefined' || !cropper) {
            return;
        }

        cropper.getCroppedCanvas({ width: CROPPED_OUTPUT_WIDTH, height: CROPPED_OUTPUT_HEIGHT, fillColor: '#fff' }).toBlob(function (blob) {
            if (!blob) return;

            // Build a File from the blob (keep original name if possible)
            const fname = 'cropped_' + Date.now() + '.jpg';
            const newFile = new File([blob], fname, { type: blob.type });

            if (cropTargetInputId) {
                const input = document.getElementById(cropTargetInputId);
                if (input && input instanceof HTMLInputElement) {
                    // Replace the File at cropTargetIndex in the input.files via DataTransfer
                    const dt = new DataTransfer();
                    const oldFiles = input.files ? Array.from(input.files) : [];
                    for (let i = 0; i < oldFiles.length; i++) {
                        if (i === cropTargetIndex) {
                            dt.items.add(newFile);
                        } else {
                            dt.items.add(oldFiles[i]);
                        }
                    }
                    // If input had no files or index out of range, just add newFile
                    if (oldFiles.length === 0) dt.items.add(newFile);

                    input.files = dt.files;

                    // Optional: update any preview UI you use for photosPreview
                    const evt = new Event('change', { bubbles: true });
                    input.dispatchEvent(evt);
                }
            }

            // Also set #CroppedPhoto hidden (keeps backward compatibility)
            const croppedHidden = document.getElementById('CroppedPhoto');
            if (croppedHidden) {
                const fr = new FileReader();
                fr.onloadend = function () { croppedHidden.value = fr.result; };
                fr.readAsDataURL(blob);
            }

            // Close modal (matches existing UI)
            const modal = document.getElementById('cropModal');
            if (modal) modal.style.display = 'none';

            // reset target
            cropTargetInputId = null;
            cropTargetIndex = 0;
        }, 'image/jpeg');
    });

})();

// --- service-card carousel initializer ---
// Call `initServiceCardCarousel()` on DOMContentLoaded and after AJAX partial replacement.
window.initServiceCardCarousel = function () {
    document.querySelectorAll('.service-card').forEach(card => {
        // avoid double-initializing the same card
        if (card.dataset.carouselInit === "1") return;
        card.dataset.carouselInit = "1";

        // Read images array from data-images attribute (fallback to any single <img> src)
        let images = [];
        try {
            const dataAttr = card.getAttribute('data-images');
            if (dataAttr) images = JSON.parse(dataAttr || '[]');
        } catch (e) {
            images = [];
        }

        const img = card.querySelector('.card-main-img') || card.querySelector('img');
        if (!img) return;

        if ((!images || images.length === 0) && img) {
            images = [img.src];
        }

        // ensure container is positioned for absolute arrows
        const container = img.parentElement || card;
        if (getComputedStyle(container).position === 'static') {
            container.style.position = 'relative';
        }

        // create arrows if missing
        function createArrow(cls, text) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'card-arrow ' + cls;
            b.setAttribute('aria-label', cls === 'left' ? 'Previous' : 'Next');
            b.textContent = text;
            return b;
        }

        let left = card.querySelector('.card-arrow.left');
        let right = card.querySelector('.card-arrow.right');
        if (!left) {
            left = createArrow('left', '◀');
            left.style.left = '8px';
            container.appendChild(left);
        }
        if (!right) {
            right = createArrow('right', '▶');
            right.style.right = '8px';
            container.appendChild(right);
        }

        // hide arrows when 0/1 image
        if (!images || images.length <= 1) {
            left.style.display = 'none';
            right.style.display = 'none';
            return;
        } else {
            left.style.display = '';
            right.style.display = '';
        }

        // closure state
        let idx = 0;
        // set current image safely
        function setImg(i) {
            idx = (i + images.length) % images.length;
            // prefer updating src only when changed to avoid unnecessary reload
            if (img.src !== images[idx]) img.src = images[idx];
        }

        // attach click handlers (remove previous handlers if any by using named functions and replacing)
        const leftHandler = (e) => { e.preventDefault(); setImg(idx - 1); };
        const rightHandler = (e) => { e.preventDefault(); setImg(idx + 1); };

        // To avoid duplicate listeners when init called multiple times, remove first.
        left.replaceWith(left.cloneNode(true));
        right.replaceWith(right.cloneNode(true));
        left = card.querySelector('.card-arrow.left');
        right = card.querySelector('.card-arrow.right');
        left.addEventListener('click', leftHandler);
        right.addEventListener('click', rightHandler);

        // initialize shown image
        setImg(0);

        // allow clicking main image to open full-size
        img.addEventListener('click', function () {
            window.open(images[idx], '_blank');
        });
    });
};

// ensure initializer runs on page load
document.addEventListener('DOMContentLoaded', function () {
    try { window.initServiceCardCarousel(); } catch (e) { console.warn('initServiceCardCarousel failed', e); }
});

// If you use unobtrusive AJAX or replace `#target`, call `window.initServiceCardCarousel()` after injecting HTML.

// In wwwroot/js/chatbox.js

document.addEventListener('DOMContentLoaded', () => {
    const chatForm = document.getElementById('chat-form');
    const userInput = document.getElementById('user-input');
    const chatHistory = document.getElementById('chat-history');

    chatForm.addEventListener('submit', async (event) => {
        event.preventDefault();
        const userQuestion = userInput.value.trim();
        if (!userQuestion) return;

        // Display user message and clear input
        addMessage('user', userQuestion);
        userInput.value = '';

        // Add a "typing" message while waiting for response
        const typingIndicator = addMessage('bot', '...', true);

        try {
            const response = await fetch('/FAQ/GetAnswer', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(userQuestion),
            });
            const data = await response.json();

            // Remove the old typing indicator
            typingIndicator.remove();
            // Display the actual answer, which may contain a hyperlink
            addMessage('bot', data.answer);

        } catch (error) {
            typingIndicator.textContent = 'An error occurred. Please try again.';
            console.error('Error fetching chat response:', error);
        }
        chatHistory.scrollTop = chatHistory.scrollHeight;
    });

    // New version of addMessage that handles hyperlinks
    function addMessage(sender, message, isTyping = false) {
        const messageElement = document.createElement('div');
        messageElement.classList.add('chat-message', `${sender}-message`);

        // Check for and format URLs
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        if (urlRegex.test(message)) {
            const formattedMessage = message.replace(urlRegex, (url) => {
                return `<a href="${url}" target="_blank">${url}</a>`;
            });
            messageElement.innerHTML = formattedMessage;
        } else {
            messageElement.textContent = message;
        }

        if (isTyping) {
            messageElement.classList.add('typing');
        }
        chatHistory.appendChild(messageElement);
        return messageElement;
    }
});

// Add this JavaScript to your payment view
document.getElementById("paymentForm").addEventListener("submit", function () {
    // Disable the button to prevent multiple submissions
    document.getElementById("payButton").disabled = true;
});

(function () {
    const paymentType = document.getElementById('PaymentType');
    const paymentMethod = document.getElementById('PaymentMethod');
    const voucherSection = document.getElementById('voucher-section');

    const summary = document.getElementById('order-summary');
    const baseSpan = document.getElementById('summary-base');
    const discountRow = document.getElementById('discount-row');
    const discountSpan = document.getElementById('summary-discount');
    const totalSpan = document.getElementById('summary-total');

    const depositNote = document.getElementById('deposit-note');
    const depositRow = document.getElementById('deposit-row');
    const remainingRow = document.getElementById('remaining-row');
    const summaryDeposit = document.getElementById('summary-deposit');
    const summaryRemaining = document.getElementById('summary-remaining');

    const baseAmount = parseFloat(summary?.dataset.base || '0');
    const fullDiscount = parseFloat(summary?.dataset.discount || '0');
    const DEPOSIT_AMOUNT = 20.00;

    function formatRM(n) {
        const v = Number.isFinite(n) ? n : 0;
        return 'RM ' + v.toFixed(2);
    }

    function show(el, yes) {
        if (!el) return;
        el.classList.toggle('pg-hidden', !yes);
    }

    function updateSummary() {
        const type = (paymentType?.value || '');
        let discount = 0;
        let total = 0;

        if (type === 'Full Payment') {
            discount = Math.min(Math.max(fullDiscount, 0), baseAmount);
            total = Math.max(0, baseAmount - discount);

            show(discountRow, true);
            show(depositRow, false);
            show(remainingRow, false);
            show(depositNote, false);

        } else if (type === 'Deposit') {
            discount = 0;
            total = DEPOSIT_AMOUNT;

            const remaining = Math.max(0, baseAmount - DEPOSIT_AMOUNT);
            if (summaryDeposit) summaryDeposit.textContent = formatRM(DEPOSIT_AMOUNT);
            if (summaryRemaining) summaryRemaining.textContent = formatRM(remaining);

            show(discountRow, false);
            show(depositRow, true);
            show(remainingRow, true);
            show(depositNote, true);
        } else {
            discount = 0;
            total = Math.max(0, baseAmount);
            show(discountRow, false);
            show(depositRow, false);
            show(remainingRow, false);
            show(depositNote, false);
        }

        if (baseSpan) baseSpan.textContent = formatRM(baseAmount);
        if (discountSpan) discountSpan.textContent = '- ' + formatRM(discount);
        if (totalSpan) totalSpan.textContent = formatRM(total);
    }

    function syncVoucherVisibility() {
        const isFull = (paymentType?.value || '') === 'Full Payment';
        const isFPX = (paymentMethod?.value || '') === 'FPX';

        // Voucher is shown only when Full Payment AND not FPX
        const allowVoucher = isFull && !isFPX;

        if (voucherSection) {
            voucherSection.classList.toggle('pg-hidden', !allowVoucher);
        }

        // Disable inputs when hidden
        const codeInput = document.getElementById('voucherCode');
        const applyBtn = document.querySelector('button[formaction$="ApplyVoucher"]');
        const removeBtn = document.querySelector('button[formaction$="RemoveVoucher"]');
        const disable = !allowVoucher;

        if (codeInput) codeInput.disabled = disable;
        if (applyBtn) applyBtn.disabled = disable;
        if (removeBtn) removeBtn.disabled = disable;

        updateSummary();
    }

    paymentType?.addEventListener('change', syncVoucherVisibility);
    paymentMethod?.addEventListener('change', syncVoucherVisibility);
    syncVoucherVisibility();
})();

