package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "players")
public class Player {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @Column(name = "clerk_user_id", nullable = false, unique = true)
    private String clerkUserId;

    @Column
    private String email;

    @Column
    private String username;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "last_login")
    private Instant lastLogin;

    protected Player() {
    }

    public Player(UUID id, String clerkUserId, String email, String username, Instant createdAt, Instant lastLogin) {
        this.id = id;
        this.clerkUserId = clerkUserId;
        this.email = email;
        this.username = username;
        this.createdAt = createdAt;
        this.lastLogin = lastLogin;
    }

    public static Player createNew(String clerkUserId, String email, String username) {
        Instant now = Instant.now();
        return new Player(UUID.randomUUID(), clerkUserId, email, username, now, now);
    }

    public void markLogin() {
        this.lastLogin = Instant.now();
    }

    public void updateProfile(String email, String username) {
        if (email != null && !email.isBlank()) {
            this.email = email;
        }
        if (username != null && !username.isBlank()) {
            this.username = username;
        }
    }

    public UUID getId() {
        return id;
    }

    public String getClerkUserId() {
        return clerkUserId;
    }

    public String getEmail() {
        return email;
    }

    public String getUsername() {
        return username;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getLastLogin() {
        return lastLogin;
    }
}
