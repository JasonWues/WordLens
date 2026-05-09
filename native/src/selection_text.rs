use std::ffi::c_char;

pub(crate) fn get_selection_text() -> *mut c_char {
    let text: String = selection::get_text();
    crate::error::string_to_c_ptr(text)
}
