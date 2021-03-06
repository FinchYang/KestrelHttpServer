// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.AspNetCore.Server.Kestrel.Tls
{
    public static class OpenSsl
    {
        public const int OPENSSL_NPN_NEGOTIATED = 1;
        public const int SSL_TLSEXT_ERR_OK = 0;
        public const int SSL_TLSEXT_ERR_NOACK = 3;
        public const int SSL_CTRL_CHAIN = 88;

        private const int BIO_C_SET_BUF_MEM_EOF_RETURN = 130;
        private const int SSL_CTRL_SET_ECDH_AUTO = 94;

        public static void SSL_library_init()
        {
            try
            {
                // Try OpenSSL 1.0.2
                NativeMethods.SSL_library_init();
            }
            catch (EntryPointNotFoundException)
            {
                // It's fine, OpenSSL 1.1 doesn't need initialization
            }
        }

        public static void SSL_load_error_strings()
        {
            try
            {
                NativeMethods.SSL_load_error_strings();
            }
            catch (EntryPointNotFoundException)
            {
                // Also fine, OpenSSL 1.1 doesn't need it.
            }
        }

        public static void OpenSSL_add_all_algorithms()
        {
            try
            {
                NativeMethods.OPENSSL_add_all_algorithms_noconf();
            }
            catch (EntryPointNotFoundException)
            {
                // Also fine, OpenSSL 1.1 doesn't need it.
            }
        }

        public static IntPtr TLSv1_2_method()
        {
            return NativeMethods.TLSv1_2_method();
        }

        public static IntPtr SSL_CTX_new(IntPtr method)
        {
            return NativeMethods.SSL_CTX_new(method);
        }

        public static void SSL_CTX_free(IntPtr ctx)
        {
            NativeMethods.SSL_CTX_free(ctx);
        }

        public unsafe static int SSL_CTX_Set_Pfx(IntPtr ctx, string path, string password)
        {
            var pass = Marshal.StringToHGlobalAnsi(password);
            var key = IntPtr.Zero;
            var cert = IntPtr.Zero;
            var ca = IntPtr.Zero;

            try
            {
                var file = System.IO.File.ReadAllBytes(path);

                fixed (void* f = file)
                {
                    var buffer = (IntPtr)f;
                    var pkcs = NativeMethods.d2i_PKCS12(IntPtr.Zero, ref buffer, file.Length);
                    var result = NativeMethods.PKCS12_parse(pkcs, pass, ref key, ref cert, ref ca);
                    if (result != 1)
                    {
                        return -1;
                    }
                    if (NativeMethods.SSL_CTX_use_certificate(ctx, cert) != 1) return -1;
                    if (NativeMethods.SSL_CTX_use_PrivateKey(ctx, key) != 1) return -1;
                    if (NativeMethods.SSL_CTX_ctrl(ctx, SSL_CTRL_CHAIN, 1, ca) != 1) return -1;
                    return 1;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pass);
                if (key != IntPtr.Zero) NativeMethods.EVP_PKEY_free(key);
                if (cert != IntPtr.Zero) NativeMethods.X509_free(cert);
                if (ca != IntPtr.Zero) NativeMethods.sk_X509_pop_free(ca);
            }
        }

        public static int SSL_CTX_set_ecdh_auto(IntPtr ctx, int onoff)
        {
            return (int)NativeMethods.SSL_CTX_ctrl(ctx, SSL_CTRL_SET_ECDH_AUTO, onoff, IntPtr.Zero);
        }

        public static int SSL_CTX_use_certificate_file(IntPtr ctx, string file, int type)
        {
            var ptr = Marshal.StringToHGlobalAnsi(file);
            var error = NativeMethods.SSL_CTX_use_certificate_file(ctx, ptr, type);
            Marshal.FreeHGlobal(ptr);

            return error;
        }

        public static int SSL_CTX_use_PrivateKey_file(IntPtr ctx, string file, int type)
        {
            var ptr = Marshal.StringToHGlobalAnsi(file);
            var error = NativeMethods.SSL_CTX_use_PrivateKey_file(ctx, ptr, type);
            Marshal.FreeHGlobal(ptr);

            return error;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate int alpn_select_cb_t(IntPtr ssl, out byte* @out, out byte outlen, byte* @in, uint inlen, IntPtr arg);

        public unsafe static void SSL_CTX_set_alpn_select_cb(IntPtr ctx, alpn_select_cb_t cb, IntPtr arg)
        {
            NativeMethods.SSL_CTX_set_alpn_select_cb(ctx, cb, arg);
        }

        public static unsafe int SSL_select_next_proto(out byte* @out, out byte outlen, byte* server, uint server_len, byte* client, uint client_len)
        {
            return NativeMethods.SSL_select_next_proto(out @out, out outlen, server, server_len, client, client_len);
        }

        public static unsafe void SSL_get0_alpn_selected(IntPtr ssl, out string protocol)
        {
            NativeMethods.SSL_get0_alpn_selected(ssl, out var data, out var length);

            protocol = data != null
                ? Marshal.PtrToStringAnsi((IntPtr)data, length)
                : null;
        }

        public static IntPtr SSL_new(IntPtr ctx)
        {
            return NativeMethods.SSL_new(ctx);
        }

        public static void SSL_free(IntPtr ssl)
        {
            NativeMethods.SSL_free(ssl);
        }

        public static int SSL_get_error(IntPtr ssl, int ret)
        {
            return NativeMethods.SSL_get_error(ssl, ret);
        }

        public static int ERR_get_error()
        {
            return NativeMethods.ERR_get_error();
        }

        public static string ERR_error_string(int error)
        {
            var buf = NativeMethods.ERR_error_string(error, IntPtr.Zero);

            // Don't free the buffer! It's a static buffer
            return Marshal.PtrToStringAnsi(buf);
        }

        public static void SSL_set_accept_state(IntPtr ssl)
        {
            NativeMethods.SSL_set_accept_state(ssl);
        }

        public static int SSL_do_handshake(IntPtr ssl)
        {
            return NativeMethods.SSL_do_handshake(ssl);
        }

        public static unsafe int SSL_read(IntPtr ssl, byte[] buffer, int offset, int count)
        {
            fixed (byte* ptr = buffer)
            {
                return NativeMethods.SSL_read(ssl, (IntPtr)(ptr + offset), count);
            }
        }

        public static unsafe int SSL_write(IntPtr ssl, byte[] buffer, int offset, int count)
        {
            fixed (byte* ptr = buffer)
            {
                return NativeMethods.SSL_write(ssl, (IntPtr)(ptr + offset), count);
            }
        }

        public static void SSL_set_bio(IntPtr ssl, IntPtr rbio, IntPtr wbio)
        {
            NativeMethods.SSL_set_bio(ssl, rbio, wbio);
        }

        public static IntPtr BIO_new(IntPtr type)
        {
            return NativeMethods.BIO_new(type);
        }

        public static unsafe int BIO_read(IntPtr b, byte[] buffer, int offset, int count)
        {
            fixed (byte* ptr = buffer)
            {
                return NativeMethods.BIO_read(b, (IntPtr)(ptr + offset), count);
            }
        }

        public static unsafe int BIO_write(IntPtr b, byte[] buffer, int offset, int count)
        {
            fixed (byte* ptr = buffer)
            {
                return NativeMethods.BIO_write(b, (IntPtr)(ptr + offset), count);
            }
        }

        public static long BIO_ctrl_pending(IntPtr b)
        {
            return NativeMethods.BIO_ctrl_pending(b);
        }

        public static long BIO_set_mem_eof_return(IntPtr b, int v)
        {
            return NativeMethods.BIO_ctrl(b, BIO_C_SET_BUF_MEM_EOF_RETURN, v, IntPtr.Zero);
        }

        public static IntPtr BIO_s_mem()
        {
            return NativeMethods.BIO_s_mem();
        }

        public static void ERR_load_BIO_strings()
        {
            NativeMethods.ERR_load_BIO_strings();
        }

        private class NativeMethods
        {
            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_library_init();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void SSL_load_error_strings();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void OPENSSL_add_all_algorithms_noconf();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr TLSv1_2_method();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SSL_CTX_new(IntPtr method);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SSL_CTX_free(IntPtr ctx);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern long SSL_CTX_ctrl(IntPtr ctx, int cmd, long larg, IntPtr parg);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_use_certificate_file(IntPtr ctx, IntPtr file, int type);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_use_PrivateKey_file(IntPtr ctx, IntPtr file, int type);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void SSL_CTX_set_alpn_select_cb(IntPtr ctx, alpn_select_cb_t cb, IntPtr arg);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe int SSL_select_next_proto(out byte* @out, out byte outlen, byte* server, uint server_len, byte* client, uint client_len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe void SSL_get0_alpn_selected(IntPtr ssl, out byte* data, out int len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SSL_new(IntPtr ctx);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SSL_free(IntPtr ssl);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_get_error(IntPtr ssl, int ret);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void SSL_set_accept_state(IntPtr ssl);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_do_handshake(IntPtr ssl);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_read(IntPtr ssl, IntPtr buf, int len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_write(IntPtr ssl, IntPtr buf, int len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void SSL_set_bio(IntPtr ssl, IntPtr rbio, IntPtr wbio);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr BIO_new(IntPtr type);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int BIO_read(IntPtr b, IntPtr buf, int len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int BIO_write(IntPtr b, IntPtr buf, int len);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern long BIO_ctrl(IntPtr bp, int cmd, long larg, IntPtr parg);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern long BIO_ctrl_pending(IntPtr bp);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr BIO_s_mem();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void ERR_load_BIO_strings();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int ERR_get_error();

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr ERR_error_string(int error, IntPtr buf);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr d2i_PKCS12(IntPtr unsused, ref IntPtr bufferPointer, long length);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int PKCS12_parse(IntPtr p12, IntPtr pass, ref IntPtr pkey, ref IntPtr cert, ref IntPtr ca);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void PKCS12_free(IntPtr p12);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void EVP_PKEY_free(IntPtr pkey);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void X509_free(IntPtr a);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern void sk_X509_pop_free(IntPtr ca);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_ctrl(IntPtr ctx, int cmd, int larg, IntPtr parg);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_set1_chain(IntPtr ctx, IntPtr sk);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_use_certificate(IntPtr ctx, IntPtr x509);

            [DllImport("libssl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int SSL_CTX_use_PrivateKey(IntPtr ctx, IntPtr pkey);
        }
    }
}
