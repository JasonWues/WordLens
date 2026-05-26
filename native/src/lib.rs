mod buffers;
mod error;
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
