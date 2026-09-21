package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "departments")
public class Department {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @Column(nullable = false, unique = true, length = 32)
    private String code;

    @Column(nullable = false)
    private String name;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    protected Department() {
    }

    public Department(UUID id, String code, String name, Instant createdAt) {
        this.id = id;
        this.code = code;
        this.name = name;
        this.createdAt = createdAt;
    }

    public static Department createNew(String code, String name) {
        return new Department(UUID.randomUUID(), code, name, Instant.now());
    }

    public UUID getId() {
        return id;
    }

    public String getCode() {
        return code;
    }

    public String getName() {
        return name;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
