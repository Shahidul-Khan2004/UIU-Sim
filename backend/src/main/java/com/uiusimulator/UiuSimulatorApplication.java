package com.uiusimulator;

import com.uiusimulator.config.DevDotEnvLoader;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;

@SpringBootApplication
@ConfigurationPropertiesScan
public class UiuSimulatorApplication {

    public static void main(String[] args) {
        DevDotEnvLoader.loadIfPresent();
        SpringApplication.run(UiuSimulatorApplication.class, args);
    }
}
