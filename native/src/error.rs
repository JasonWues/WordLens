use std::cell::RefCell;
use std::ffi::{c_char, CString};

thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

pub(crate) fn set_last_error(message: impl Into<String>) {
    LAST_ERROR.with(|error| {
        *error.borrow_mut() = Some(message.into());
    });
}

pub(crate) fn clear_last_error() {
    LAST_ERROR.with(|error| {
        *error.borrow_mut() = None;
    });
}

pub(crate) fn string_to_c_ptr(text: String) -> *mut c_char {
    let sanitized = text.replace('\0', "");
    match CString::new(sanitized) {
        Ok(cstr) => cstr.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

pub(crate) fn free_c_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }

    unsafe {
        let _ = CString::from_raw(ptr);
    }
}

pub(crate) fn get_last_native_error() -> *mut c_char {
    let message = LAST_ERROR
        .with(|error| error.borrow().clone())
        .unwrap_or_default();

    string_to_c_ptr(message)
}
