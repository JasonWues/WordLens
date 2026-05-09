mod buffers;
mod error;
mod ocr;
mod screenshot;
mod selection_text;

use buffers::{ByteBuffer, ScreenshotBuffer, VirtualScreenBounds};
use error::{clear_last_error, set_last_error};

#[unsafe(no_mangle)]
pub extern "C" fn get_selection_text() -> *mut std::ffi::c_char {
    selection_text::get_selection_text()
}

#[unsafe(no_mangle)]
pub extern "C" fn free_c_string(ptr: *mut std::ffi::c_char) {
    error::free_c_string(ptr);
}

#[unsafe(no_mangle)]
pub extern "C" fn get_last_native_error() -> *mut std::ffi::c_char {
    error::get_last_native_error()
}

#[unsafe(no_mangle)]
pub extern "C" fn capture_screen_region(
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

    unsafe {
        *out_buffer = ScreenshotBuffer::empty();
    }

    match std::panic::catch_unwind(|| screenshot::capture_region(x, y, width, height)) {
        Ok(Ok(buffer)) => {
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
pub extern "C" fn free_screenshot_buffer(data: *mut u8, len: usize, capacity: usize) {
    buffers::free_byte_allocation(data, len, capacity);
}

#[unsafe(no_mangle)]
pub extern "C" fn free_byte_buffer(data: *mut u8, len: usize, capacity: usize) {
    buffers::free_byte_allocation(data, len, capacity);
}

#[unsafe(no_mangle)]
pub extern "C" fn preprocess_ocr_bgra_to_png(
    pixels: *const u8,
    width: u32,
    height: u32,
    stride: u32,
    out_buffer: *mut ByteBuffer,
) -> i32 {
    if out_buffer.is_null() {
        set_last_error("Output byte buffer pointer is null");
        return -1;
    }

    unsafe {
        *out_buffer = ByteBuffer::empty();
    }

    match std::panic::catch_unwind(|| ocr::preprocess_bgra_to_png(pixels, width, height, stride)) {
        Ok(Ok(buffer)) => {
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
            set_last_error("OCR image preprocessing panicked");
            -3
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn get_virtual_screen_bounds(out_bounds: *mut VirtualScreenBounds) -> i32 {
    if out_bounds.is_null() {
        set_last_error("Output bounds pointer is null");
        return -1;
    }

    match std::panic::catch_unwind(screenshot::virtual_screen_bounds) {
        Ok(Ok(bounds)) => {
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
