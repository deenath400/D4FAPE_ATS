import NextAuth from "next-auth";
import CredentialsProvider from "next-auth/providers/credentials";
import { invokeBackend } from "@/lib/server/backend-invoke";

export type AuthResponseDto = {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  user: {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    roles: string[];
  };
};

export const { handlers, signIn, signOut, auth } = NextAuth({
  providers: [
    CredentialsProvider({
      name: "Credentials",
      credentials: {
        email: { label: "Email", type: "email" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        if (!credentials?.email || !credentials?.password) {
          return null;
        }

        try {
          const data = await invokeBackend<AuthResponseDto>({
            path: "/api/auth/login",
            init: {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({
                email: credentials.email,
                password: credentials.password,
              }),
            },
          });

          return {
            id: data.user.id,
            email: data.user.email,
            firstName: data.user.firstName,
            lastName: data.user.lastName,
            roles: data.user.roles,
            accessToken: data.accessToken,
            refreshToken: data.refreshToken,
            expiresIn: data.expiresIn,
          };
        } catch {
          return null;
        }
      },
    }),
  ],
  session: {
    strategy: "jwt",
  },
  callbacks: {
    async jwt({ token, user }) {
      if (user) {
        token.accessToken = user.accessToken;
        token.refreshToken = user.refreshToken;
        token.accessTokenExpires = Date.now() + user.expiresIn * 1000;
        token.user = {
          id: user.id,
          email: user.email,
          firstName: user.firstName,
          lastName: user.lastName,
          roles: user.roles,
        };
        return token;
      }

      const expires = typeof token.accessTokenExpires === "number" ? token.accessTokenExpires : 0;
      if (expires > 0 && Date.now() < expires) {
        return token;
      }

      try {
        const refreshedData = await invokeBackend<AuthResponseDto>({
          path: "/api/auth/refresh",
          init: {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken: token.refreshToken }),
          },
        });

        return {
          ...token,
          accessToken: refreshedData.accessToken,
          refreshToken: refreshedData.refreshToken,
          accessTokenExpires: Date.now() + refreshedData.expiresIn * 1000,
          user: {
            id: refreshedData.user.id,
            email: refreshedData.user.email,
            firstName: refreshedData.user.firstName,
            lastName: refreshedData.user.lastName,
            roles: refreshedData.user.roles,
          },
        };
      } catch {
        return { ...token, error: "RefreshAccessTokenError" };
      }
    },
    async session({ session, token }) {
      const userObj = token.user as
        | {
            id: string;
            email: string;
            firstName: string;
            lastName: string;
            roles: string[];
          }
        | undefined;

      if (userObj) {
        session.user = {
          ...session.user,
          id: userObj.id,
          email: userObj.email,
          firstName: userObj.firstName,
          lastName: userObj.lastName,
          roles: userObj.roles,
        };
      }
      session.accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.refreshToken = typeof token.refreshToken === "string" ? token.refreshToken : undefined;
      session.error = typeof token.error === "string" ? token.error : undefined;
      return session;
    },
  },
  secret: process.env.AUTH_SECRET || "DevelopmentSecretKeyAtLeast32BytesLengthForNextAuth!",
});
