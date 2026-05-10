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

#[cfg(test)]
mod tests {
    use std::ffi::CStr;

    use super::*;

    fn ptr_to_string(ptr: *mut c_char) -> String {
        let text = unsafe { CStr::from_ptr(ptr) }
            .to_string_lossy()
            .into_owned();
        free_c_string(ptr);
        text
    }

    #[test]
    fn string_to_c_ptr_removes_interior_nul_bytes() {
        let ptr = string_to_c_ptr("ab\0cd".to_string());

        assert!(!ptr.is_null());
        assert_eq!(ptr_to_string(ptr), "abcd");
    }

    #[test]
    fn last_error_can_be_set_read_and_cleared() {
        set_last_error("native failure");

        assert_eq!(ptr_to_string(get_last_native_error()), "native failure");

        clear_last_error();

        assert_eq!(ptr_to_string(get_last_native_error()), "");
    }

    #[test]
    fn free_c_string_accepts_null() {
        free_c_string(std::ptr::null_mut());
    }
}
