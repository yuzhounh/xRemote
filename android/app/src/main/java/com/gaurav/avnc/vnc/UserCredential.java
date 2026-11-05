/* SPDX-License-Identifier: GPL-3.0-or-later
 * ABI adapter for AVNC 3.3.1 (https://github.com/gujjwal00/avnc).
 */
package com.gaurav.avnc.vnc;

public final class UserCredential {
    public final String username, password;
    public UserCredential(String username, String password) { this.username=username; this.password=password; }
}
