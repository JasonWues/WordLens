mod buffers;
mod error;
#[cfg(target_os = "macos")]
mod ocr;
mod screenshot;
mod selection_text;

use buffers::{ScreenshotBuffer, VirtualScreenBounds};
use error::{clear_last_error, set_last_error};

#[unsafe(no_mangle)]
pub extern "C" fn get_selection_text() -> *mut std::ffi::c_char {
    selection_text::get_selection_text()
}

#[unsafe(no_mangle)]
/// # Safety
///
/// `ptr` must be null or a pointer previously returned by this library from
/// `get_selection_text` or `get_last_native_error`. It must not be freed more
/// than once.
pub unsafe extern "C" fn free_c_string(ptr: *mut std::ffi::c_char) {
    // SAFETY: The caller upholds the ownership contract documented above.
    unsafe {
        error::free_c_string(ptr);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn get_last_native_error() -> *mut std::ffi::c_char {
    error::get_last_native_error()
}

#[unsafe(no_mangle)]
/// # Safety
///
/// `out_buffer` must be a valid, writable pointer to a `ScreenshotBuffer` for
/// the duration of this call.
pub unsafe extern "C" fn capture_screen_region(
    x: i32,
    y: i32,
    width: u32,
    height: u32,
    out_buffer: *mut ScreenshotBuffer,
) -> i32 {
    if out_buffer.is_null() {
        set_last_error("Output buffer pointer is null");
        return -1;
    }

    // SAFETY: `out_buffer` was checked for null and the caller guarantees it is writable.
    unsafe {
        *out_buffer = ScreenshotBuffer::empty();
    }

    match std::panic::catch_unwind(|| screenshot::capture_region(x, y, width, height)) {
        Ok(Ok(buffer)) => {
            // SAFETY: `out_buffer` was checked for null and the caller guarantees it is writable.
            unsafe {
                *out_buffer = buffer;
            }
            clear_last_error();
            0
        }
        Ok(Err(err)) => {
            set_last_error(err);
            -2
        }
        Err(_) => {
            set_last_error("xcap capture panicked");
            -3
        }
    }
}

#[unsafe(no_mangle)]
/// # Safety
///
/// `data`, `len`, and `capacity` must match a buffer previously returned by
/// this library in `ScreenshotBuffer`. The buffer must not be freed more than
/// once.
pub unsafe extern "C" fn free_screenshot_buffer(data: *mut u8, len: usize, capacity: usize) {
    // SAFETY: The caller upholds the allocation ownership contract documented above.
    unsafe {
        buffers::free_byte_allocation(data, len, capacity);
    }
}

#[unsafe(no_mangle)]
/// # Safety
///
/// `out_bounds` must be a valid, writable pointer to a `VirtualScreenBounds`
/// for the duration of this call.
pub unsafe extern "C" fn get_virtual_screen_bounds(out_bounds: *mut VirtualScreenBounds) -> i32 {
    if out_bounds.is_null() {
        set_last_error("Output bounds pointer is null");
        return -1;
    }

    match std::panic::catch_unwind(screenshot::virtual_screen_bounds) {
        Ok(Ok(bounds)) => {
            // SAFETY: `out_bounds` was checked for null and the caller guarantees it is writable.
            unsafe {
                *out_bounds = bounds;
            }
            clear_last_error();
            0
        }
        Ok(Err(err)) => {
            set_last_error(err);
            -2
        }
        Err(_) => {
            set_last_error("xcap monitor enumeration panicked");
            -3
        }
    }
}

/// Splits a comma-separated, NUL-terminated C string of language codes into a
/// list, trimming whitespace and dropping empty entries. A null pointer yields
/// an empty list (meaning "auto-detect").
#[cfg(target_os = "macos")]
fn parse_language_list(languages: *const std::ffi::c_char) -> Vec<String> {
    if languages.is_null() {
        return Vec::new();
    }

    // SAFETY: The caller guarantees a valid NUL-terminated string when non-null.
    let raw = unsafe { std::ffi::CStr::from_ptr(languages) };
    raw.to_string_lossy()
        .split(',')
        .map(str::trim)
        .filter(|code| !code.is_empty())
        .map(str::to_string)
        .collect()
}

#[unsafe(no_mangle)]
/// Recognizes text in a PNG image using the macOS Vision framework.
///
/// # Safety
///
/// `data` must point to `len` readable bytes, or be null when `len` is 0.
/// `languages` must be null or a valid NUL-terminated UTF-8 C string of
/// comma-separated BCP-47 codes. The returned pointer is either null (on
/// failure; call `get_last_native_error`) or a UTF-8 string that must be freed
/// with `free_c_string`.
pub unsafe extern "C" fn recognize_text_macos(
    data: *const u8,
    len: usize,
    languages: *const std::ffi::c_char,
) -> *mut std::ffi::c_char {
    #[cfg(target_os = "macos")]
    {
        if data.is_null() || len == 0 {
            set_last_error("OCR input image is empty");
            return std::ptr::null_mut();
        }

        // SAFETY: The caller guarantees `data` points to `len` readable bytes.
        let bytes = unsafe { std::slice::from_raw_parts(data, len) };
        let languages = parse_language_list(languages);

        match std::panic::catch_unwind(|| ocr::recognize(bytes, &languages)) {
            Ok(Ok(text)) => {
                clear_last_error();
                error::string_to_c_ptr(text)
            }
            Ok(Err(err)) => {
                set_last_error(err);
                std::ptr::null_mut()
            }
            Err(_) => {
                set_last_error("Vision OCR panicked");
                std::ptr::null_mut()
            }
        }
    }

    #[cfg(not(target_os = "macos"))]
    {
        let _ = (data, len, languages);
        set_last_error("Vision OCR is only available on macOS");
        std::ptr::null_mut()
    }
}

#[unsafe(no_mangle)]
/// Returns the Vision-supported recognition languages as a comma-separated,
/// UTF-8 C string (BCP-47 codes). Returns null on failure (call
/// `get_last_native_error`). The returned pointer must be freed with
/// `free_c_string`.
pub extern "C" fn vision_ocr_supported_languages() -> *mut std::ffi::c_char {
    #[cfg(target_os = "macos")]
    {
        match std::panic::catch_unwind(ocr::supported_languages) {
            Ok(Ok(languages)) => {
                clear_last_error();
                error::string_to_c_ptr(languages.join(","))
            }
            Ok(Err(err)) => {
                set_last_error(err);
                std::ptr::null_mut()
            }
            Err(_) => {
                set_last_error("Vision OCR panicked");
                std::ptr::null_mut()
            }
        }
    }

    #[cfg(not(target_os = "macos"))]
    {
        set_last_error("Vision OCR is only available on macOS");
        std::ptr::null_mut()
    }
}
